// The identity GitHub Actions deploys with (ADR 0021): a user-assigned managed identity federated with GitHub's
// OIDC issuer for one repository environment, so the workflow needs no stored credential. Roles are the minimum for a
// deployment: read the resource group (deployment outputs), push images (AcrPush) and fetch cluster credentials (AKS Cluster Admin; the cluster uses Kubernetes
// RBAC with local accounts, so admin credentials are the way in).

param location string
param tags object
param identityName string

@description('GitHub repository in owner/name form.')
param gitHubRepository string

@description('Numeric id of the repository owner (gh api users/<owner> --jq .id).')
param gitHubOwnerId string

@description('Numeric id of the repository (gh api repos/<owner>/<name> --jq .id).')
param gitHubRepositoryId string

@description('GitHub environment the deploy job runs in; the token subject names it.')
param gitHubEnvironment string = 'dev'

param registryName string
param clusterName string

// Built-in roles.
var readerRoleId = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'
var acrPushRoleId = '8311e382-0749-4cb8-b61a-304f252e45ec'
var aksClusterAdminRoleId = '0ab0b1a8-8aac-4efd-b8c2-3ee1fb270be8'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

// GitHub's OIDC subject for a job that targets an environment is "repo:<owner>@<ownerId>/<name>@<repoId>:environment:<env>";
// Entra matches it verbatim, so the ids are required. The presented subject is printed by azure/login when it fails.
var owner = split(gitHubRepository, '/')[0]
var repositoryName = split(gitHubRepository, '/')[1]

resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-${owner}-${repositoryName}-${gitHubEnvironment}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${owner}@${gitHubOwnerId}/${repositoryName}@${gitHubRepositoryId}:environment:${gitHubEnvironment}'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: registryName
}

resource cluster 'Microsoft.ContainerService/managedClusters@2024-10-01' existing = {
  name: clusterName
}

// Reader on the resource group: the workflow reads the "main" deployment outputs and looks up the registry and
// cluster by name before pushing and applying.
resource resourceGroupReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, identity.id, readerRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', readerRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, identity.id, acrPushRoleId)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPushRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource clusterAdmin 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cluster.id, identity.id, aksClusterAdminRoleId)
  scope: cluster
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', aksClusterAdminRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output clientId string = identity.properties.clientId
