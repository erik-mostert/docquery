<#
.SYNOPSIS
Builds the DocQuery images, pushes them to the registry and applies the Kubernetes manifests to the AKS cluster
that infra/bicep/main.bicep created (ADR 0021).

.DESCRIPTION
Reads the platform's names from the "main" deployment outputs, so the only input is the resource group.
Steps: az acr login; dotnet publish -t:PublishContainer for the five .NET hosts; docker build/push for the web app;
render deploy/k8s/overlays/dev from its templates; az aks get-credentials; kubectl apply -k; wait for rollouts;
print the ingress address. Requires: Azure CLI (logged in), .NET SDK, Docker, kubectl.

.EXAMPLE
./deploy/scripts/deploy.ps1 -ResourceGroup rg-docquery-dev
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [string] $Tag = (git rev-parse --short HEAD),
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 compatible: two-argument Join-Path and BOM-free file writes (kustomize rejects a BOM).
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$overlay = Join-Path $root 'deploy\k8s\overlays\dev'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Get-Output([string] $name) {
    $value = az deployment group show -g $ResourceGroup -n main --query "properties.outputs.$name.value" -o tsv
    if (-not $value) { throw "Deployment output '$name' is empty; run infra/bicep/main.bicep first." }
    return $value
}

Write-Host "Reading platform outputs from resource group $ResourceGroup"
$registry = Get-Output 'containerRegistryLoginServer'
$registryName = Get-Output 'containerRegistryName'
$cluster = Get-Output 'aksClusterName'
$workloadClientId = Get-Output 'workloadIdentityClientId'
$keyVaultUri = Get-Output 'keyVaultUri'
$blobEndpoint = Get-Output 'blobEndpoint'
$serviceBusHost = Get-Output 'serviceBusHostName'
$openAiEndpoint = Get-Output 'openAiEndpoint'

if (-not $SkipBuild) {
    Write-Host "Logging in to $registry"
    az acr login --name $registryName | Out-Null

    $hosts = @(
        'src/backend/DocQuery.Command.Api',
        'src/backend/DocQuery.Query.Api',
        'src/backend/DocQuery.Workers.OutboxRelay',
        'src/backend/DocQuery.Workers.Chunking',
        'src/backend/DocQuery.Workers.Embedding'
    )
    foreach ($project in $hosts) {
        Write-Host "Publishing $project as a container ($Tag)"
        dotnet publish (Join-Path $root $project) -c Release -t:PublishContainer `
            -p:ContainerRegistry=$registry -p:ContainerImageTag=$Tag -p:ContainerRuntimeIdentifier=linux-x64 --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
    }

    Write-Host "Building the web image ($Tag)"
    $webImage = "$registry/docquery/web:$Tag"
    docker build -t $webImage (Join-Path $root 'src\clients\docquery.web')
    if ($LASTEXITCODE -ne 0) { throw 'Building the web image failed.' }
    docker push $webImage
    if ($LASTEXITCODE -ne 0) { throw 'Pushing the web image failed.' }
}

Write-Host "Rendering overlay $overlay"
$kustomization = Get-Content (Join-Path $overlay 'kustomization.template.yaml') -Raw
$kustomization = $kustomization.Replace('{{REGISTRY}}', $registry).Replace('{{TAG}}', $Tag).Replace('{{WORKLOAD_CLIENT_ID}}', $workloadClientId)
[System.IO.File]::WriteAllText((Join-Path $overlay 'kustomization.yaml'), $kustomization, $utf8NoBom)

$envFile = Get-Content (Join-Path $overlay 'dev.env.template') -Raw
$envFile = $envFile.Replace('{{KEYVAULT_URI}}', $keyVaultUri).Replace('{{BLOB_ENDPOINT}}', $blobEndpoint).Replace('{{SERVICEBUS_HOST}}', $serviceBusHost).Replace('{{OPENAI_ENDPOINT}}', $openAiEndpoint)
[System.IO.File]::WriteAllText((Join-Path $overlay 'dev.env'), $envFile, $utf8NoBom)

Write-Host "Fetching credentials for $cluster"
az aks get-credentials -g $ResourceGroup -n $cluster --admin --overwrite-existing | Out-Null

Write-Host 'Applying manifests'
kubectl apply -k $overlay
if ($LASTEXITCODE -ne 0) { throw 'kubectl apply failed.' }

foreach ($deployment in 'command-api', 'query-api', 'outbox-relay', 'chunking-worker', 'embedding-worker', 'web') {
    kubectl rollout status deployment/$deployment -n docquery --timeout=300s
    if ($LASTEXITCODE -ne 0) { throw "Rollout of $deployment did not complete." }
}

# The managed ingress assigns its public IP asynchronously, usually within a minute or two of the first apply.
$ip = ''
for ($attempt = 0; $attempt -lt 36 -and -not $ip; $attempt++) {
    $ip = kubectl get ingress docquery -n docquery -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
    if (-not $ip) { Start-Sleep -Seconds 5 }
}

Write-Host ''
if ($ip) {
    Write-Host "DocQuery is available at http://$ip/"
} else {
    Write-Warning 'The ingress has no address yet. Check: kubectl get ingress docquery -n docquery; kubectl get svc -n app-routing-system'
}
