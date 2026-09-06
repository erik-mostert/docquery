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
