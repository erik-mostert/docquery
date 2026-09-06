# Deploying DocQuery to AKS

The platform (registry, cluster, identities and everything else) comes from `infra/bicep`. This directory holds
what runs on the cluster and how it gets there. Design notes: [ADR 0021](../docs/adr/0021-aks-with-workload-identity-and-sdk-container-images.md).

| Path | Contents |
|---|---|
| `k8s/base/` | Environment-independent manifests: namespace and service account, the two APIs, three workers, the web app, one ingress |
| `k8s/overlays/dev/` | Kustomize overlay for the dev cluster: image names and tag, workload identity client id, the `docquery-config` ConfigMap. `kustomization.yaml` and `dev.env` are rendered from the `*.template` files |
| `scripts/deploy.ps1` | Builds and pushes the images, renders the overlay, applies it and waits for the rollout |
| `../.github/workflows/deploy.yml` | The same steps on every push to `main`, after build and tests |

## How the services are configured in the cluster

Every host loads `docquery-config` as environment variables and Key Vault as a configuration source below them
(ADR 0017's ordering). The ConfigMap carries identity-based connection strings for Blob Storage, Service Bus and
Azure OpenAI (endpoints only), which take precedence over the key-based ones the vault holds for local runs.
Only `ConnectionStrings:docquery` (PostgreSQL, with its password) is read from the vault. Pods run as the
`docquery` service account, annotated with the workload identity's client id; the AKS webhook injects a federated
token that `DefaultAzureCredential` exchanges for Azure tokens. No secret is ever stored in a manifest.

Traffic: the managed NGINX ingress serves the UI at `/` and forwards `/api/command/*` and `/api/query/*` to the
APIs with the prefix stripped, so the browser uses one origin and CORS is not involved. The web image is built
with `VITE_API_URL=/api/command` and `VITE_QUERY_API_URL=/api/query`.

## First deployment from a workstation

Prerequisites: the Bicep deployment done (`infra/README.md`), Azure CLI logged in, .NET 10 SDK, Docker Desktop,
kubectl (`az aks install-cli`).

```powershell
./deploy/scripts/deploy.ps1 -ResourceGroup rg-docquery-dev
```

The script prints the ingress address at the end; open it in a browser. Images are tagged with the current git
short SHA; pass `-Tag` to override, or `-SkipBuild` to only re-apply manifests.

Commit the rendered `k8s/overlays/dev/kustomization.yaml` and `dev.env`: they contain resource names and
endpoints, not secrets, and the workflow renders the same files.

## GitHub Actions

The workflow authenticates with OpenID Connect against the deploy identity that `main.bicep` creates when
`gitHubRepository` is set. The federated credential must match the subject GitHub presents for the deploy job,
`repo:<owner>@<ownerId>/<name>@<repoId>:environment:dev`, so the parameter file also carries `gitHubOwnerId` and
`gitHubRepositoryId` (`gh api users/<owner> --jq .id`, `gh api repos/<owner>/<name> --jq .id`). A failed
`azure/login` step prints the exact subject it presented. Configure the repository once:

| Kind | Name | Value |
|---|---|---|
| Secret | `AZURE_CLIENT_ID` | `deployIdentityClientId` output of the `main` deployment |
| Secret | `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| Secret | `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
| Variable | `AZURE_RESOURCE_GROUP` | `rg-docquery-dev` |

Create a `dev` environment in the repository settings (the deploy job targets it, which is where approvals or
branch restrictions would go). Pull requests run build and tests only.

## Operating

```powershell
kubectl get pods -n docquery                       # all six deployments Running
kubectl logs deployment/embedding-worker -n docquery -f
kubectl get ingress docquery -n docquery           # public IP
az aks stop -g rg-docquery-dev -n <cluster>        # pause the node pool between demos; az aks start to resume
```

Migrations run when the command API starts, which is why it has one replica. Scale the workers with
`kubectl scale deployment/chunking-worker -n docquery --replicas=2`; the Service Bus subscriptions and the
outbox's row locking make that safe.

Not yet done: HTTPS on the ingress (a DNS name plus a certificate), Application Insights export from the pods,
and disabling key-based access on Storage, Service Bus and OpenAI now that the cluster uses identities.
