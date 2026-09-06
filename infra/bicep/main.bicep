// Step 2 of 2: the DocQuery platform, deployed into one resource group after bootstrap.bicep created the Key Vault
// and the PostgreSQL password was placed in it. AKS and the container registry are added with the Kubernetes step.
//
//   az deployment group create --resource-group rg-docquery-dev --template-file main.bicep --parameters main.bicepparam
//
// The PostgreSQL password is read from Key Vault with getSecret() and never passes through a parameter or output.
// Resource-generated keys are written back into the same vault as ready-to-use connection strings.

targetScope = 'resourceGroup'

import { resourceToken, keyVaultName, postgresAdminPasswordSecretName } from 'modules/naming.bicep'

@description('Short environment name used in resource names, e.g. docquery-dev. Must match bootstrap.bicep.')
@minLength(3)
@maxLength(24)
param environmentName string

@description('Region for everything except Azure OpenAI.')
param location string = resourceGroup().location

@description('Region for the Azure OpenAI account; model availability differs per region.')
param openAiLocation string = location

@description('PostgreSQL administrator login. The password is the Key Vault secret "postgres-admin-password".')
param postgresAdminLogin string = 'docquery'

@description('Optional public IP allowed through the PostgreSQL firewall for local tooling (pgAdmin). Empty to skip.')
param clientIpAddress string = ''

@description('Embedding model deployment.')
param embeddingModel object = {
  name: 'text-embedding-3-small'
  version: '1'
  capacity: 50
}

@description('Chat model deployment for the RAG query step.')
param chatModel object = {
  name: 'gpt-4o-mini'
  version: '2024-07-18'
  capacity: 10
}

@description('Tags applied to every resource.')
param tags object = {
  project: 'docquery'
  environment: environmentName
}

var token = resourceToken(resourceGroup().id)

// Names are computed here (not taken from module outputs) so the existing-resource references below can call
// listKeys() and getSecret() at deployment start.
var vaultName = keyVaultName(environmentName, token)
// uniqueString() always yields 13 characters, so this is 15+ characters; the compiler cannot infer that (BCP334).
var storageAccountName = 'st${replace(environmentName, '-', '')}${token}'
var serviceBusNamespaceName = 'sb-${environmentName}-${token}'
var openAiAccountName = 'oai-${environmentName}-${token}'
var documentsContainerName = 'documents'

// Created by bootstrap.bicep; holds the human-provided password and receives the generated secrets.
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: vaultName
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    tags: tags
    workspaceName: 'log-${environmentName}'
    appInsightsName: 'appi-${environmentName}'
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    tags: tags
    #disable-next-line BCP334
    storageAccountName: storageAccountName
    documentsContainerName: documentsContainerName
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: {
    location: location
    tags: tags
    namespaceName: serviceBusNamespaceName
    topicName: 'document-events'
    subscriptionNames: [
      'chunking'
      'embedding'
    ]
    maxDeliveryCount: 5
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    location: location
    tags: tags
    serverName: 'psql-${environmentName}-${token}'
    databaseName: 'docquery'
    administratorLogin: postgresAdminLogin
    administratorPassword: keyVault.getSecret(postgresAdminPasswordSecretName)
    clientIpAddress: clientIpAddress
  }
}

module openAi 'modules/openai.bicep' = {
  name: 'openai'
  params: {
    location: openAiLocation
    tags: tags
    accountName: openAiAccountName
    embeddingModel: embeddingModel
    chatModel: chatModel
  }
}

// Existing-resource references so the generated keys can be read and handed to the secrets module.
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  #disable-next-line BCP334
  name: storageAccountName
  dependsOn: [
    storage
  ]
}

resource serviceBusRootRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2024-01-01' existing = {
  name: '${serviceBusNamespaceName}/RootManageSharedAccessKey'
  dependsOn: [
    serviceBus
  ]
}

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: openAiAccountName
  dependsOn: [
    openAi
  ]
}

module keyVaultSecrets 'modules/keyvault-secrets.bicep' = {
  name: 'keyvault-secrets'
  params: {
    vaultName: vaultName
    documentsContainerName: documentsContainerName
    storageAccountName: storageAccountName
    storageAccountKey: storageAccount.listKeys().keys[0].value
    openAiEndpoint: openAi.outputs.endpoint
    openAiKey: openAiAccount.listKeys().key1
    serviceBusHostName: serviceBus.outputs.hostName
    serviceBusConnectionString: serviceBusRootRule.listKeys().primaryConnectionString
    postgresHost: postgres.outputs.fullyQualifiedDomainName
    postgresDatabase: postgres.outputs.databaseName
    postgresLogin: postgresAdminLogin
    postgresPassword: keyVault.getSecret(postgresAdminPasswordSecretName)
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
  }
}

// Non-secret outputs only. Every secret lives in Key Vault.
output keyVaultName string = vaultName
output keyVaultUri string = keyVault.properties.vaultUri
output storageAccountName string = storage.outputs.storageAccountName
output blobEndpoint string = storage.outputs.blobEndpoint
output documentsContainerName string = storage.outputs.documentsContainerName
output serviceBusNamespaceName string = serviceBus.outputs.namespaceName
output serviceBusHostName string = keyVaultSecrets.outputs.serviceBusHostName
output postgresServerName string = postgres.outputs.serverName
output postgresFullyQualifiedDomainName string = postgres.outputs.fullyQualifiedDomainName
output postgresDatabaseName string = postgres.outputs.databaseName
output openAiAccountName string = openAi.outputs.accountName
output openAiEndpoint string = openAi.outputs.endpoint
output embeddingDeploymentName string = openAi.outputs.embeddingDeploymentName
output chatDeploymentName string = openAi.outputs.chatDeploymentName
output logAnalyticsWorkspaceId string = monitoring.outputs.workspaceId
