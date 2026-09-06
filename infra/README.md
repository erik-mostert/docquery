# Infrastructure

Bicep templates for the Azure resources DocQuery uses. One resource group holds everything; the templates are
idempotent, so re-running a deployment applies changes in place. AKS and the container registry arrive with the
Kubernetes step and will be added here.

Deployment is two steps because of how secrets flow:

1. **`bootstrap.bicep`** creates the Key Vault. You place the one human-provided secret, the PostgreSQL
   administrator password, in it by hand.
2. **`main.bicep`** deploys the platform. It reads that password from the vault with `getSecret()`, so it never
   passes through a parameter, an environment variable or an output, and it writes the resource-generated keys
   back into the same vault as ready-to-use connection strings.

| File | Resources |
|---|---|
| `bicep/modules/keyvault.bicep` | Key Vault (RBAC, soft delete) and an optional Secrets Officer assignment for you |
| `bicep/modules/monitoring.bicep` | Log Analytics workspace, Application Insights (workspace-based) |
| `bicep/modules/storage.bicep` | Storage account (LRS, TLS 1.2, no public blobs), `documents` container, soft delete and change feed on |
| `bicep/modules/servicebus.bicep` | Service Bus namespace (Standard), topic `document-events` with duplicate detection, subscriptions `chunking` and `embedding` (5 deliveries, dead-letter on expiry) |
| `bicep/modules/postgres.bicep` | PostgreSQL Flexible Server (B1ms, 32 GB), `vector` extension allow-listed, `docquery` database, firewall rules |
| `bicep/modules/openai.bicep` | Azure OpenAI account with `text-embedding-3-small` (Standard) and `gpt-4o-mini` (Global Standard) deployments |
| `bicep/modules/keyvault-secrets.bicep` | Writes the connection strings below into the vault |
| `bicep/modules/naming.bicep` | Name functions shared by both deployments |

Secrets in the vault and the configuration keys they map to (the `--` convention):

| Secret | Configuration key | Used by |
|---|---|---|
| `postgres-admin-password` | (read by Bicep only) | PostgreSQL server |
| `ConnectionStrings--openai` | `ConnectionStrings:openai` | embedding worker, query API |
| `ConnectionStrings--documents` | `ConnectionStrings:documents` | command API, chunking worker |
| `ConnectionStrings--servicebus` | `ConnectionStrings:servicebus` | relay and workers |
| `ConnectionStrings--docquery` | `ConnectionStrings:docquery` | every .NET host |
| `ApplicationInsights--ConnectionString` | `ApplicationInsights:ConnectionString` | telemetry export (later step) |

## Prerequisites

- Azure CLI 2.60+ with Bicep (`az bicep install`), logged in (`az login`) to the target subscription.
- Azure OpenAI access enabled on the subscription and quota in the chosen region.
- Resource names are `<prefix>-<environmentName>-<8-char hash of the resource group id>`, e.g. `kv-docqry-dev-abc12def`, so they are stable across
  deployments and globally unique; both parameter files must use the same `environmentName` (12 characters at most).

## Deploy

PowerShell, from the repository root.

Step 1, the vault:

```powershell
$env:KEYVAULT_ADMIN_OBJECT_ID = (az ad signed-in-user show --query id -o tsv)   # grants you Secrets Officer
$rg = 'rg-docquery-dev'

az group create --name $rg --location westeurope
az deployment group create --resource-group $rg --template-file infra/bicep/bootstrap.bicep --parameters infra/bicep/bootstrap.bicepparam

$vault = az deployment group show -g $rg -n bootstrap --query properties.outputs.keyVaultName.value -o tsv
az keyvault secret set --vault-name $vault --name postgres-admin-password --value (Read-Host -AsSecureString 'PostgreSQL admin password' | ConvertFrom-SecureString -AsPlainText)
```

Step 2, the platform:

```powershell
$env:CLIENT_IP = (Invoke-RestMethod https://api.ipify.org)   # optional: lets pgAdmin reach the server

az deployment group create --resource-group $rg --template-file infra/bicep/main.bicep --parameters infra/bicep/main.bicepparam
```

Preview changes without applying them:

```powershell
az deployment group what-if --resource-group $rg --template-file infra/bicep/main.bicep --parameters infra/bicep/main.bicepparam
```

If the OpenAI deployment fails with a model-availability or quota error, set `openAiLocation` in
`main.bicepparam` to a region that has the models (for example `swedencentral`); everything else stays put.

## After deployment: connecting the local services

Only the vault URI is needed locally; every secret comes from Key Vault through your `az login` identity. Store
the URI as an AppHost user secret:

```powershell
$vaultUri = az deployment group show -g $rg -n main --query properties.outputs.keyVaultUri.value -o tsv
dotnet user-secrets set "ConnectionStrings:keyvault" $vaultUri --project src/DocQuery.AppHost
```

The embedding step wires the workers to read the vault as a configuration source (Aspire Key Vault integration),
so `ConnectionStrings:openai` resolves without copying the key anywhere.

To inspect or rotate a secret by hand:

```powershell
az keyvault secret show --vault-name $vault --name ConnectionStrings--openai --query value -o tsv
```

Rotating a key on the source resource and re-running `main.bicep` rewrites the derived secret; changing the
PostgreSQL password means updating `postgres-admin-password` in the vault and re-running `main.bicep`, which
applies it to the server and rewrites `ConnectionStrings--docquery`. Running services pick changes up on restart.

## Cost and teardown

Standing cost is dominated by PostgreSQL B1ms and the Service Bus Standard base charge (roughly tens of euros a
month combined); OpenAI is pay-per-token and idle deployments cost nothing on Standard tiers; Key Vault is
negligible. Remove everything with:

```powershell
az group delete --name $rg --yes --no-wait
az keyvault purge --name $vault    # soft-deleted vaults keep their name reserved for 7 days
```

## Conventions

- One module per service; `main.bicep` only wires names, tags, parameters and the secrets module.
- Human-provided secrets live in Key Vault before the platform is deployed and are read with `getSecret()`;
  resource-generated keys are written to the vault by the deployment. Templates never output secrets.
- Local auth (keys, SAS, passwords) is enabled for the development environment. The Kubernetes step switches
  services to managed identities and disables local auth where possible; Key Vault access then goes through
  workload identity with the Secrets User role instead of a personal assignment.
- API versions are pinned; bump deliberately and re-run `az bicep build` to catch schema changes.
