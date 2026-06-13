// Local-mode resources for the Lab 502 Community Hub (.NET native deployment):
//  - App Service Plan (Linux)
//  - Web App (.NET 10 native, no containers)
// Everything is intended to be deployed to a single resource group.

@description('Base name used to derive resource names. Lowercase letters and digits.')
@minLength(3)
@maxLength(20)
param baseName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('SKU name for the Linux App Service Plan.')
param appServicePlanSku string = 'B1'

@description('Internal port the app listens on.')
param appPort int = 1345

@description('Optional explicit Web App name (must be globally unique). When empty, a unique name is generated from baseName.')
param webAppName string = ''

var planName = '${baseName}-plan'
var appName = empty(webAppName) ? '${baseName}-app-${uniqueString(resourceGroup().id)}' : webAppName

module planModule 'modules/plan.bicep' = {
  name: 'plan'
  params: {
    location: location
    name: planName
    sku: appServicePlanSku
  }
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: planModule.outputs.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServicePlanSku != 'F1' && appServicePlanSku != 'D1'
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'true'
        }
        {
          name: 'PORT'
          value: string(appPort)
        }
      ]
    }
  }
}

output webAppName string = app.name
output webAppUrl string = 'https://${app.properties.defaultHostName}'
output resourceGroupName string = resourceGroup().name
