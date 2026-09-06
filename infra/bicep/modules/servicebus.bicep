// Service Bus namespace with the document-events topic and one subscription per worker (ADR 0014).
// Standard tier is the cheapest tier that supports topics.

param location string
param tags object
param namespaceName string
param topicName string
param subscriptionNames array

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

resource subscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = [
  for subscriptionName in subscriptionNames: {
    parent: topic
    name: subscriptionName
    properties: {
      maxDeliveryCount: maxDeliveryCount
      lockDuration: 'PT1M'
      deadLetteringOnMessageExpiration: true
      deadLetteringOnFilterEvaluationExceptions: true
      defaultMessageTimeToLive: 'P14D'
    }
  }
]

output namespaceName string = serviceBusNamespace.name
output hostName string = '${serviceBusNamespace.name}.servicebus.windows.net'
output topicName string = topic.name
