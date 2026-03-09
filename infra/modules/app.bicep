// Azure App Service module
// Deploys App Service Plan and Web App for the RAG Agent API

@description('Name of the App Service')
param name string

@description('Name of the App Service Plan')
param appServicePlanName string

@description('Location for the resources')
param location string

@description('App Service Plan SKU')
@allowed(['B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P1v2', 'P2v2', 'P3v2', 'P1v3', 'P2v3', 'P3v3'])
param sku string = 'B2'

@description('Runtime stack')
param linuxFxVersion string = 'DOTNETCORE|8.0'

@description('Tags to apply to the resources')
param tags object = {}

// App settings
@description('OpenAI endpoint')
param openAiEndpoint string = ''

@description('OpenAI API key')
@secure()
param openAiApiKey string = ''

@description('PostgreSQL connection string')
@secure()
param postgresConnectionString string = ''

@description('Storage connection string')
@secure()
param storageConnectionString string = ''

@description('ACS connection string')
@secure()
param acsConnectionString string = ''

@description('Application Insights connection string')
param appInsightsConnectionString string = ''

@description('Key Vault URI')
param keyVaultUri string = ''

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: sku
  }
  properties: {
    reserved: true // Required for Linux
  }
}

// Web App for API
resource webAppApi 'Microsoft.Web/sites@2023-01-01' = {
  name: name
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      alwaysOn: sku != 'B1' // AlwaysOn not available on Basic tier
      http20Enabled: true
      minTlsVersion: '1.2'
      webSocketsEnabled: true // For SignalR
      cors: {
        allowedOrigins: [
          'https://${name}-ui.azurewebsites.net'
          'http://localhost:5173'
          'https://localhost:7170'
        ]
        supportCredentials: true
      }
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'AzureOpenAI__Endpoint'
          value: openAiEndpoint
        }
        {
          name: 'AzureOpenAI__ApiKey'
          value: openAiApiKey
        }
        {
          name: 'AzureOpenAI__ChatDeployment'
          value: 'gpt-35-turbo'
        }
        {
          name: 'AzureOpenAI__EmbeddingDeployment'
          value: 'text-embedding-ada-002'
        }
        {
          name: 'ConnectionStrings__PostgreSql'
          value: postgresConnectionString
        }
        {
          name: 'ConnectionStrings__AzureStorage'
          value: storageConnectionString
        }
        {
          name: 'ACS__ConnectionString'
          value: acsConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVault__Uri'
          value: keyVaultUri
        }
      ]
    }
  }
}

// Web App for UI (Blazor)
resource webAppUi 'Microsoft.Web/sites@2023-01-01' = {
  name: '${name}-ui'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      alwaysOn: sku != 'B1'
      http20Enabled: true
      minTlsVersion: '1.2'
      webSocketsEnabled: true // For Blazor Server
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ApiSettings__BaseUrl'
          value: 'https://${webAppApi.properties.defaultHostName}'
        }
        {
          name: 'ApiSettings__SignalRHub'
          value: 'https://${webAppApi.properties.defaultHostName}/chathub'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ]
    }
  }
}

// Outputs
@description('API Web App hostname')
output apiHostname string = webAppApi.properties.defaultHostName

@description('API Web App URL')
output apiUrl string = 'https://${webAppApi.properties.defaultHostName}'

@description('UI Web App hostname')
output uiHostname string = webAppUi.properties.defaultHostName

@description('UI Web App URL')
output uiUrl string = 'https://${webAppUi.properties.defaultHostName}'

@description('API Web App resource ID')
output apiId string = webAppApi.id

@description('UI Web App resource ID')
output uiId string = webAppUi.id

@description('API Web App name')
output apiName string = webAppApi.name

@description('UI Web App name')
output uiName string = webAppUi.name

@description('API managed identity principal ID')
output apiPrincipalId string = webAppApi.identity.principalId

@description('UI managed identity principal ID')
output uiPrincipalId string = webAppUi.identity.principalId

@description('App Service Plan ID')
output appServicePlanId string = appServicePlan.id
