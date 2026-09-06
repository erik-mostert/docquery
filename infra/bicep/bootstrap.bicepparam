// Step 1 of 2 (see bootstrap.bicep).
//
//   $env:KEYVAULT_ADMIN_OBJECT_ID = (az ad signed-in-user show --query id -o tsv)   # lets you manage the secrets
//
using 'bootstrap.bicep'

param environmentName = 'docqry-dev'
param location = 'westeurope'
param keyVaultAdministratorPrincipalId = readEnvironmentVariable('KEYVAULT_ADMIN_OBJECT_ID', '')
