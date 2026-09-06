// Azure OpenAI account with an embedding deployment (embedding worker) and a chat deployment (RAG query API).
// Deployments on one account must be created one at a time, hence the explicit dependency.

param location string
param tags object
param accountName string

@description('{ name, version, sku, capacity } of the embedding model; capacity is thousands of tokens per minute.')
param embeddingModel object

@description('Whether to create the chat deployment (only the query API uses it).')
param deployChatModel bool

@description('{ name, version, sku, capacity } of the chat model.')
param chatModel object

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false // keys are used locally; the Kubernetes step moves to managed identity
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: account
  name: embeddingModel.name
  sku: {
    name: embeddingModel.sku
    capacity: embeddingModel.capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: embeddingModel.name
      version: embeddingModel.version
    }
    // Pinned: an automatic upgrade recreates the deployment and returns DeploymentNotFound for a while. Bump the
    // version in the parameters and redeploy when a newer model version is wanted.
    versionUpgradeOption: 'NoAutoUpgrade'
  }
}

resource chatDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployChatModel) {
  parent: account
  name: chatModel.name
  sku: {
    name: chatModel.sku
    capacity: chatModel.capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: chatModel.name
      version: chatModel.version
    }
    // Pinned: an automatic upgrade recreates the deployment and returns DeploymentNotFound for a while. Bump the
    // version in the parameters and redeploy when a newer model version is wanted.
    versionUpgradeOption: 'NoAutoUpgrade'
  }
  dependsOn: [
    embeddingDeployment
  ]
}

output accountName string = account.name
output endpoint string = account.properties.endpoint
output embeddingDeploymentName string = embeddingDeployment.name
output chatDeploymentName string = deployChatModel ? chatModel.name : ''
