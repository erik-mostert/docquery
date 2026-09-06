// Key Vault with RBAC authorization. Created by bootstrap.bicep before the platform, so human-provided secrets
// (the PostgreSQL password) can be placed in it and read by main.bicep with getSecret().

param location string
param tags object
param vaultName string

@description('Object id of the deployer (or a group) granted Key Vault Secrets Officer. Empty to skip the assignment.')
param administratorPrincipalId string = ''

// Built-in role: Key Vault Secrets Officer (read, write, delete secrets; no key or certificate rights).
var secretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    // Lets Azure Resource Manager read secrets referenced with getSecret() during a deployment (main.bicep reads
    // the PostgreSQL password). Without it the deployment fails with "Access denied to first party service".
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    // Off for the development environment so a deleted vault can be purged and its name reused. Turn on for
    // production; it cannot be turned off again once enabled.
    enablePurgeProtection: null
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource administratorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(administratorPrincipalId)) {
  name: guid(vault.id, administratorPrincipalId, secretsOfficerRoleId)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficerRoleId)
    principalId: administratorPrincipalId
    principalType: 'User'
  }
}

output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri
