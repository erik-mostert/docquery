// Names shared by bootstrap.bicep and main.bicep so both deployments address the same resources.

@export()
func resourceToken(resourceGroupId string) string => toLower(uniqueString(resourceGroupId))

// Vault names: 3-24 chars, alphanumerics and hyphens, globally unique.
@export()
func keyVaultName(environmentName string, token string) string => 'kv-${take(replace(environmentName, '-', ''), 8)}-${token}'

// Name of the secret a person creates once in the vault before the platform deployment (see ../README.md).
@export()
var postgresAdminPasswordSecretName = 'postgres-admin-password'
