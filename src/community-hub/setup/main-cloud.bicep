// Cloud-mode resources for the Lab 502 Community Hub (.NET native deployment):
//  - Azure SQL Server + Database (Entra-only auth)
//  - Azure Storage Account (blob, public-read containers)
//  - Log Analytics Workspace + Application Insights
//  - App Service Plan + Web App (.NET 10 native, no containers)
//  - Role assignments: Storage Blob Data Contributor, Monitoring Metrics Publisher
//
// All Azure access uses the Web App's system-assigned managed identity.
// No keys, passwords, SAS tokens, or connection strings are emitted.

@description('Base name used to derive resource names. Lowercase letters and digits.')
@minLength(3)
@maxLength(20)
param baseName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Tenant id for the active tenant. Lowercase letters, digits, hyphens.')
@minLength(1)
@maxLength(31)
param appTenant string

@description('Internal port the app listens on.')
param appPort int = 1345

@description('SKU name for the Linux App Service Plan.')
param appServicePlanSku string = 'B1'

@description('Optional explicit Web App name (must be globally unique).')
param webAppName string = ''

@description('SQL Database name.')
param sqlDatabase string = 'dashboard'

@description('SQL Database service objective (SKU). S1 = 20 DTUs.')
param sqlServiceObjective string = 'S1'

// ── Derived names ───────────────────────────────────────────────────────────

var sanitized = toLower(replace(baseName, '-', ''))
var suffix = uniqueString(resourceGroup().id)
var storageAccountName = take('${sanitized}st${suffix}', 24)
var sqlServerName = '${baseName}-sql-${suffix}'
var logAnalyticsName = '${baseName}-logs'
var appInsightsName = '${baseName}-ai'
var planName = '${baseName}-plan'
var appName = empty(webAppName) ? '${baseName}-app-${suffix}' : webAppName

// ── Storage Account + Blob Containers ───────────────────────────────────────

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource screenshotsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'screenshots'
  properties: {
    publicAccess: 'Blob'
  }
}

resource galleryContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'gallery'
  properties: {
    publicAccess: 'Blob'
  }
}

// ── Azure SQL Server + Database ─────────────────────────────────────────────

#disable-next-line use-secure-value-for-secure-inputs
var sqlBootstrapPassword = 'B!${uniqueString(resourceGroup().id, sqlServerName)}x1'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'yourInitLogin'
    administratorLoginPassword: sqlBootstrapPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlAadAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: app.name
    sid: app.identity.principalId
    tenantId: subscription().tenantId
  }
}

resource sqlAadOnly 'Microsoft.Sql/servers/azureADOnlyAuthentications@2023-08-01-preview' = {
  parent: sqlServer
  name: 'Default'
  dependsOn: [sqlAadAdmin]
  properties: {
    azureADOnlyAuthentication: true
  }
}

resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabase
  location: location
  sku: {
    name: sqlServiceObjective
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// ── Log Analytics + Application Insights ────────────────────────────────────

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ── App Service Plan + Web App (.NET native) ────────────────────────────────

var blobPublicBase = 'https://${storageAccount.name}.blob.${environment().suffixes.storage}'

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
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: planModule.outputs.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServicePlanSku != 'F1' && appServicePlanSku != 'D1'
      appSettings: [
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: 'false' }
        { name: 'APP_MODE', value: 'cloud' }
        { name: 'APP_TENANT', value: appTenant }
        { name: 'PORT', value: string(appPort) }
        { name: 'SQL_SERVER', value: sqlServer.properties.fullyQualifiedDomainName }
        { name: 'SQL_DATABASE', value: sqlDatabase }
        { name: 'AZURE_STORAGE_ACCOUNT', value: storageAccount.name }
        { name: 'AZURE_BLOB_PUBLIC_BASE', value: blobPublicBase }
        { name: 'LAB502_DASHBOARD_URL', value: 'https://${appName}.azurewebsites.net' }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
      ]
    }
  }
}

// ── Role Assignments ────────────────────────────────────────────────────────

var storageBlobDataContributorRole = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource storageBlobRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, app.id, storageBlobDataContributorRole)
  scope: storageAccount
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRole)
  }
}

var monitoringMetricsPublisherRole = '3913510d-42f4-4e42-8a64-420c390055eb'

resource monitoringRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appInsights.id, app.id, monitoringMetricsPublisherRole)
  scope: appInsights
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRole)
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────

output webAppName string = app.name
output webAppUrl string = 'https://${app.properties.defaultHostName}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output storageAccountName string = storageAccount.name
output blobPublicBase string = blobPublicBase
output resourceGroupName string = resourceGroup().name
output activeTenant string = appTenant
