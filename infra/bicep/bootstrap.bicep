// Step 1 of 2: the Key Vault that holds human-provided secrets. Deploy this first, put the PostgreSQL
// administrator password in it, then deploy main.bicep, which reads the password with getSecret().
//
//   az deployment group create --resource-group rg-docquery-dev --template-file bootstrap.bicep --parameters bootstrap.bicepparam
//   az keyvault secret set --vault-name <keyVaultName output> --name postgres-admin-password --value '<strong password>'

targetScope = 'resourceGroup'

import { resourceToken, keyVaultName } from 'modules/naming.bicep'

@description('Short environment name used in resource names, e.g. docqry-dev (12 characters at most so every derived name fits its limit). Must match main.bicep.')
@minLength(3)
@maxLength(12)
param environmentName string

param location string = resourceGroup().location

@description('Object id of the user or group that may read and write Key Vault secrets. Empty to skip.')
param keyVaultAdministratorPrincipalId string = ''

param tags object = {
  project: 'docquery'
  environment: environmentName
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    tags: tags
    vaultName: keyVaultName(environmentName, resourceToken(resourceGroup().id))
    administratorPrincipalId: keyVaultAdministratorPrincipalId
  }
}

output keyVaultName string = keyVault.outputs.vaultName
output keyVaultUri string = keyVault.outputs.vaultUri
