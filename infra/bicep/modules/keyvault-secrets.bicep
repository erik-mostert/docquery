// Writes the resource-generated connection strings into the existing Key Vault. Secret names use "--" so they map
// onto configuration keys ("ConnectionStrings--openai" -> ConnectionStrings:openai) when a service loads the vault
// as a configuration source. The PostgreSQL password is read from the same vault by main.bicep and only passes
// through here to be composed into a connection string.

param vaultName string
param documentsContainerName string
param storageAccountName string
param openAiEndpoint string
param serviceBusHostName string
param postgresHost string
param postgresDatabase string
param postgresLogin string
param applicationInsightsConnectionString string

@secure()
param storageAccountKey string

@secure()
param openAiKey string

@secure()
param serviceBusConnectionString string

@secure()
param postgresPassword string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: vaultName
}

var secrets = [
  {
    name: 'ConnectionStrings--openai'
    value: 'Endpoint=${openAiEndpoint};Key=${openAiKey}'
  }
  {
    name: 'ConnectionStrings--documents'
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccountKey};EndpointSuffix=${environment().suffixes.storage};ContainerName=${documentsContainerName}'
  }
  {
    name: 'ConnectionStrings--servicebus'
    value: serviceBusConnectionString
  }
  {
    name: 'ConnectionStrings--docquery'
    value: 'Host=${postgresHost};Database=${postgresDatabase};Username=${postgresLogin};Password=${postgresPassword};SSL Mode=Require'
  }
  {
    name: 'ApplicationInsights--ConnectionString'
    value: applicationInsightsConnectionString
  }
]

resource secretResources 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = [
  for secret in secrets: {
    parent: vault
    name: secret.name
    properties: {
      value: secret.value
      contentType: 'text/plain'
    }
  }
]

output serviceBusHostName string = serviceBusHostName
