// Service Bus namespace with the document-events topic and one subscription per worker (ADR 0014). Each
// subscription receives only its own message type through a correlation filter on Subject (ADR 0018).
// Standard tier is the cheapest tier that supports topics.

param location string
param tags object
param namespaceName string
param topicName string

@description('One entry per worker: { name, subject }. subject is the contract type full name the subscription accepts.')
param subscriptions array

@minValue(1)
@maxValue(2000)
param maxDeliveryCount int = 5

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
    disableLocalAuth: false // SAS keys are used locally; the Kubernetes step moves to managed identity
    publicNetworkAccess: 'Enabled'
  }
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: serviceBusNamespace
  name: topicName
  properties: {
    // The outbox id is the MessageId, so duplicate detection drops republished messages (ADR 0014).
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    defaultMessageTimeToLive: 'P14D'
    supportOrdering: false
  }
}

resource topicSubscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = [
  for subscription in subscriptions: {
    parent: topic
    name: subscription.name
    properties: {
      maxDeliveryCount: maxDeliveryCount
      lockDuration: 'PT1M'
      deadLetteringOnMessageExpiration: true
      deadLetteringOnFilterEvaluationExceptions: true
      defaultMessageTimeToLive: 'P14D'
    }
  }
]

// One explicit correlation rule per subscription, matching on Subject (ARM: "label"). Declaring a rule through ARM
// leaves the subscription without the catch-all "$Default" rule, so only matching messages get through. Naming
// the rule "$Default" instead does not work: Azure ends up with no rule at all, which discards every message.
resource subjectRules 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = [
  for (subscription, index) in subscriptions: {
    parent: topicSubscriptions[index]
    name: 'subject'
    properties: {
      filterType: 'CorrelationFilter'
      correlationFilter: {
        label: subscription.subject
      }
    }
  }
]

output namespaceName string = serviceBusNamespace.name
output hostName string = '${serviceBusNamespace.name}.servicebus.windows.net'
output topicName string = topic.name
