# 0016. Azure resources are defined in Bicep, one module per service, with secrets in Key Vault

Date: 2026-09-06

## Status

Accepted

## Context

The embedding worker needs Azure OpenAI, which has no local emulator, and the demo's stated goal includes AZ-104
and AZ-305 material: provisioning, naming, networking basics, identity. Clicking resources together in the portal
leaves nothing reviewable or repeatable, and the Kubernetes step will need the same resources again with identity
wiring.

## Decision

- Infrastructure lives in `infra/bicep`, deployed at resource-group scope with `az deployment group create`.
  `main.bicep` composes one module per service (monitoring, storage, service bus, postgres, openai) and only
  handles names, tags and parameter plumbing.
- Names are `<type>-<environmentName>-<hash of resource group id>`; the hash makes globally unique names stable
  across redeployments without hand-picking suffixes.
- Secrets are `@secure()` parameters fed from environment variables via `main.bicepparam`, which is therefore
  safe to commit. Templates never output secrets.
- Secrets flow in two directions, which is why deployment is two steps. Human-provided secrets (the PostgreSQL
  administrator password) are placed in a Key Vault created first by `bootstrap.bicep` and read by `main.bicep`
  with `getSecret()`, so they never pass through parameters, environment variables or outputs. Resource-generated
  keys (storage, Service Bus, OpenAI), which only exist after the resources do, are read by the deployment with
  `listKeys()` and written to the same vault as complete connection strings named after configuration keys
  (`ConnectionStrings--openai` maps to `ConnectionStrings:openai`). Locally, the only value a developer stores is
  the vault URI; services load the vault as a configuration source through their `az login` identity. In
  Kubernetes the same vault is read through workload identity with the Secrets User role.
- Both templates import name functions from `modules/naming.bicep` so they address the same vault.
- Azure OpenAI gets its own `openAiLocation` parameter because model availability differs per region; the rest of
  the platform stays in `location`.
- Configuration parity with the Aspire AppHost: container `documents`, topic `document-events`, subscriptions
  `chunking` and `embedding` with `maxDeliveryCount` 5, PostgreSQL 17 with the `vector` extension allow-listed.
  Extras that only make sense in Azure are enabled here: blob soft delete and change feed, topic duplicate detection
  keyed by the outbox `MessageId`.
- The development environment keeps local authentication (storage keys, SAS, database password, OpenAI keys)
  enabled and public network access on, with the PostgreSQL firewall limited to Azure services plus an optional
  client IP. The Kubernetes step replaces keys with managed identities and revisits network exposure.
- Sizing is the smallest paid tier everywhere (PostgreSQL B1ms, Service Bus Standard, OpenAI S0 with modest
  capacity). Standing cost is documented in `infra/README.md` together with teardown.

## Consequences

- One command creates or updates the whole environment; `what-if` previews changes.
- No Azure Developer CLI (`azd`) or Aspire publish/deploy integration yet; the templates are plain Bicep so they
  work with any pipeline. Aspire's own manifest-based deployment can be layered on later.
- Deployments are not run from this repository's CI; the author deploys from a logged-in shell.
- Every platform deployment rewrites the generated secrets, creating a new secret version even when the value is
  unchanged. Rotating a key on its resource therefore requires a redeploy to refresh the vault. Managed identity
  in the Kubernetes step removes most of these keys entirely.
- API versions are pinned and validated only by `az bicep build` plus a real deployment; there is no test harness
  for infrastructure beyond linting.

## Alternatives considered

- **Terraform.** Rejected: Bicep is the exam-relevant, first-party tool and needs no state backend.
- **Aspire `azd` integration (Azure Container Apps).** Rejected for now: the target is Kubernetes, not Container
  Apps, and the resources should be readable without Aspire's generated manifest.
- **Portal-created resources.** Rejected: not reviewable, not repeatable.
- **Copying keys into user secrets per developer.** Rejected: keys spread across machines with no rotation story;
  Key Vault centralises them and rotation is a redeploy.
- **Key Vault references in App Configuration.** Deferred: App Configuration is a reasonable next layer for
  non-secret settings but adds a service the demo does not need yet.
