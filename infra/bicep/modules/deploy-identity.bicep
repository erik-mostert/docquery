// The identity GitHub Actions deploys with (ADR 0021): a user-assigned managed identity federated with GitHub's
// OIDC issuer for one repository branch, so the workflow needs no stored credential. Roles are the minimum for a
// deployment: push images (AcrPush) and fetch cluster credentials (AKS Cluster Admin; the cluster uses Kubernetes
// RBAC with local accounts, so admin credentials are the way in).

param location string
param tags object
param identityName string

@description('GitHub repository in owner/name form.')
param gitHubRepository string

param gitHubBranch string = 'main'
param registryName string
param clusterName string

// Built-in roles.
var acrPushRoleId = '8311e382-0749-4cb8-b61a-304f252e45ec'
var aksClusterAdminRoleId = '0ab0b1a8-8aac-4efd-b8c2-3ee1fb270be8'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-${replace(gitHubRepository, '/', '-')}-${gitHubBranch}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${gitHubRepository}:ref:refs/heads/${gitHubBranch}'
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
