# 0021. AKS with workload identity, SDK-built container images and Kustomize manifests

Date: 2026-09-06

## Status

Accepted

## Context

Every service exists and runs under Aspire; the remaining goal from ADR 0002 is to run them in Kubernetes on
Azure, as the demo's AZ-104 and AZ-305 material. Constraints: the platform is already in Bicep with secrets in
Key Vault (ADR 0016), the hosts read the vault below their environment variables (ADR 0017), the project owner
chose AKS over lighter options, and the cluster must not hold any credential that a leaked manifest or repository
would expose.

## Decision

**Cluster.** `aks.bicep` creates a free-tier AKS cluster with one system node pool (`Standard_B2ms` × 2 by
default), Azure CNI overlay, the OIDC issuer and workload identity enabled, the application routing add-on
(managed NGINX ingress) and Container Insights into the existing Log Analytics workspace. `containerregistry.bicep`
adds a Basic registry with the admin user off; the kubelet identity gets AcrPull. Kubernetes RBAC with local
accounts is kept; Entra integration is a follow-up.

**Identities instead of keys.** `workload-identity.bicep` creates the identity the pods run as and federates it
with the cluster's issuer for the `docquery` service account. It holds Key Vault Secrets User, Storage Blob Data
Contributor, Service Bus Data Sender and Receiver, and Cognitive Services OpenAI User. In the cluster the
`docquery-config` ConfigMap supplies endpoint-only connection strings for Blob Storage, Service Bus and OpenAI;
because environment variables outrank the vault, these replace the key-based strings the vault keeps for local
use. PostgreSQL still needs its password, so `ConnectionStrings:docquery` is the one value read from the vault,
which every host now loads when `ConnectionStrings:keyvault` is present. `deploy-identity.bicep` creates a
second identity, federated with GitHub's OIDC issuer for the `main` branch, holding AcrPush and AKS Cluster
Admin, so the pipeline stores no credential either.

**Images.** The five .NET hosts are published with `dotnet publish /t:PublishContainer`
(`EnableSdkContainerSupport` plus a `ContainerRepository` per project): no Dockerfiles, Microsoft base images,
non-root user, one command in the script and the workflow. The web app is the exception: a two-stage Dockerfile
builds the Vite bundle with relative API paths and serves it with nginx.

**Manifests.** Kustomize with a base and a dev overlay. The base carries Deployments and Services for the six
components, the namespace, the service account and one Ingress that routes `/` to the UI and `/api/command/*`
and `/api/query/*` to the APIs with the prefix stripped, so the browser sees one origin. The overlay sets image
names and tag, the service account's client id and the ConfigMap, and is rendered from templates by
`deploy/scripts/deploy.ps1` or the workflow using the Bicep outputs; the rendered files are committed because
they hold names, not secrets. The command API keeps one replica because it applies migrations at startup.

**Pipeline.** `.github/workflows/deploy.yml` builds and tests on every push and pull request, and on `main`
pushes images tagged with the commit SHA and applies the overlay. Azure login is OIDC; the repository holds only
the deploy identity's client id, the tenant and subscription ids, and the resource group name.

## Consequences

- No secret exists outside Key Vault: not in manifests, not in the repository, not in GitHub. Rotating the
  storage or Service Bus key affects nothing in the cluster.
- Local runs are unchanged: without a vault URI the hosts behave exactly as before, and the vault's key-based
  strings still serve developers who point Aspire at Azure.
- Cost is dominated by the node pool; `az aks stop` pauses it between demos. The free control plane has no SLA,
  fine for a demo.
- Not covered yet: HTTPS on the ingress, Application Insights export, Entra-integrated cluster RBAC, disabling
  local auth on the data services, autoscaling. Each is additive.
- `dotnet test` in the pipeline runs the Testcontainers suites on the runner's Docker, which makes the test job
  slower than the unit tests alone; acceptable for the demo's traffic.

## Alternatives considered

- **Azure Container Apps.** Less to operate and Aspire can deploy to it directly, but the goal is to show
  Kubernetes; AKS with the managed ingress and workload identity keeps the operational surface small anyway.
- **Secrets Store CSI driver.** Mounts vault secrets as files or Kubernetes Secrets. The hosts already read the
  vault as a configuration source with the same identity, so the driver would add a component without removing
  one.
- **Dockerfiles for the .NET hosts.** Familiar, but five near-identical files to maintain; the SDK produces the
  same layered image from the project itself.
- **Helm.** Templating power the six components do not need; Kustomize overlays cover the per-environment
  differences with plain YAML.
- **Entra ID for PostgreSQL.** Would remove the last password, but token-based Npgsql authentication needs a
  refresh mechanism in the connection setup; deferred until the rest is stable.
