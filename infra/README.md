# Infrastructure

Bicep templates for the Azure resources DocQuery uses. One resource group holds everything; the templates are
idempotent, so re-running a deployment applies changes in place.

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
| `bicep/modules/openai.bicep` | Azure OpenAI account with an embedding deployment (`text-embedding-3-small`) and a chat deployment (`gpt-5.4-mini`), both Global Standard; model, version and SKU are parameters of `main.bicep` |
| `bicep/modules/keyvault-secrets.bicep` | Writes the connection strings below into the vault |
| `bicep/modules/containerregistry.bicep` | Container registry (Basic, no admin user) |
| `bicep/modules/aks.bicep` | AKS (free tier, one system pool) with OIDC issuer, workload identity, managed NGINX ingress and Container Insights; AcrPull for the kubelet |
| `bicep/modules/workload-identity.bicep` | The identity the pods run as: federated with the cluster for the `docquery` service account; Key Vault Secrets User, Storage Blob Data Contributor, Service Bus Data Sender and Receiver, Cognitive Services OpenAI User |
| `bicep/modules/deploy-identity.bicep` | The identity GitHub Actions deploys with (OIDC federation for the `dev` environment of the repository): Reader on the resource group, AcrPush and AKS Cluster Admin. Skipped when `gitHubRepository` is empty |
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
- Resource providers registered once per subscription (AKS with Container Insights needs all of them):

  ```powershell
  foreach ($ns in 'Microsoft.OperationsManagement', 'Microsoft.OperationalInsights', 'Microsoft.ContainerService', 'Microsoft.ContainerRegistry', 'Microsoft.ManagedIdentity') { az provider register --namespace $ns --wait }
  ```
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

Outputs the Kubernetes step uses (`containerRegistryLoginServer`, `aksClusterName`, `workloadIdentityClientId`,
`deployIdentityClientId`) are read by `deploy/scripts/deploy.ps1` and the GitHub workflow; see
[deploy/README.md](../deploy/README.md) for the repository secrets the workflow needs.

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

Standing cost is dominated by the AKS node pool (two `Standard_B2ms` nodes by default, `aksNodeCount` and
`aksNodeSize` in `main.bicep`), PostgreSQL B1ms and the Service Bus Standard base charge; OpenAI is pay-per-token
and idle deployments cost nothing on Standard tiers; the registry, Key Vault and the free AKS control plane are
negligible. Stop the cluster between demos with `az aks stop -g $rg -n <cluster>` (nodes are deallocated, the
public IP is kept) and `az aks start` to resume. Remove everything with:

```powershell
az group delete --name $rg --yes --no-wait
az keyvault purge --name $vault    # soft-deleted vaults keep their name reserved for 7 days
```

## Conventions

- One module per service; `main.bicep` only wires names, tags, parameters and the secrets module.
- Human-provided secrets live in Key Vault before the platform is deployed and are read with `getSecret()`;
  resource-generated keys are written to the vault by the deployment. Templates never output secrets.
- Local auth (keys, SAS, passwords) stays enabled so the key-based connection strings in the vault keep working
  for local runs. In the cluster the services use the workload identity for Blob Storage, Service Bus and Azure
  OpenAI and read only the PostgreSQL connection string from the vault (ADR 0021); disabling local auth on those
  resources is a follow-up once nothing else uses the keys.
- API versions are pinned; bump deliberately and re-run `az bicep build` to catch schema changes.
