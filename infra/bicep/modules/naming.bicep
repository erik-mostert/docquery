// Names shared by bootstrap.bicep and main.bicep so both deployments address the same resources.
//
// Every globally unique resource is named '<prefix>-<environmentName>-<token>' (storage: no hyphens). The token is
// 8 characters of uniqueString(resource group id); with environmentName capped at 12 characters every name stays
// within the strictest limit, 24 characters for Key Vault (3 + 12 + 1 + 8) and storage accounts (2 + 12 + 8).

@export()
func resourceToken(resourceGroupId string) string => take(toLower(uniqueString(resourceGroupId)), 8)

@export()
func keyVaultName(environmentName string, token string) string => 'kv-${environmentName}-${token}'

@export()
func storageName(environmentName string, token string) string => 'st${replace(environmentName, '-', '')}${token}'

// Name of the secret a person creates once in the vault before the platform deployment (see ../README.md).
@export()
var postgresAdminPasswordSecretName = 'postgres-admin-password'
