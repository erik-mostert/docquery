// Step 2 of 2 (see main.bicep). No secrets here: the PostgreSQL password is read from the Key Vault created by
// bootstrap.bicep.
//
//   $env:CLIENT_IP = (Invoke-RestMethod https://api.ipify.org)   # optional, for pgAdmin
//
using 'main.bicep'

param environmentName = 'docqry-dev'
param location = 'westeurope'
// Azure OpenAI model availability varies by region; point this elsewhere (e.g. swedencentral) if a deployment
// fails with a "model not available" error.
param openAiLocation = 'westeurope'
param postgresAdminLogin = 'docquery'
param clientIpAddress = readEnvironmentVariable('CLIENT_IP', '')
// The chat model is only needed by the query API. This subscription has no quota for the template's default
// (gpt-5.4-mini) but 500K TPM Global Standard for gpt-5-mini, so that one is deployed. Check with
// `az cognitiveservices usage list -l westeurope` or the Quota page in Azure AI Foundry (Show all).
param deployChatModel = true
param chatModel = {
  name: 'gpt-5-mini'
  version: '2025-08-07'
  sku: 'GlobalStandard'
  capacity: 10
}

// Lets the GitHub Actions workflow in .github/workflows deploy through OIDC (no stored credential). Empty skips it.
// The ids are part of the token subject GitHub presents: gh api users/erik-mostert --jq .id, gh api repos/erik-mostert/docquery --jq .id
param gitHubRepository = 'erik-mostert/docquery'
param gitHubOwnerId = '45812744'
param gitHubRepositoryId = '1357385689'
param gitHubEnvironment = 'dev'
