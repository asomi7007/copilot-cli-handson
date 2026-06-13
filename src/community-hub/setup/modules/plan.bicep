@description('Azure region for the plan.')
param location string

@description('Name for the App Service Plan.')
param name string

@description('SKU name for the Linux App Service Plan (e.g. B1, S1, P1v3).')
param sku string = 'B1'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: name
  location: location
  sku: {
    name: sku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

output id string = plan.id
output name string = plan.name
