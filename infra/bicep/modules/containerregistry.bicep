// Container registry for the DocQuery images. Basic tier is enough for a handful of repositories; images are pushed
// by the deploy identity (AcrPush) and pulled by the AKS kubelet identity (AcrPull, assigned in aks.bicep).

param location string
param tags object

@minLength(5)
@maxLength(50)
param registryName string

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false // identities only; no username/password pulls
    publicNetworkAccess: 'Enabled'
  }
}

output registryName string = registry.name
output loginServer string = registry.properties.loginServer
