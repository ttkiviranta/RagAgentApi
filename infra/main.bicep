// RAG Agent API - Main Bicep Template
// Orchestrates deployment of all Azure resources

@description('Environment name (dev, test, prod)')
@allowed(['dev', 'test', 'prod'])
param environment string = 'dev'

@description('Azure region for resources')
param location string = resourceGroup().location

@description('Base name for resources')
param baseName string = 'ragagent'

@description('PostgreSQL administrator password')
@secure()
param postgresAdminPassword string

@description('Enable Document Intelligence deployment')
param enableDocumentIntelligence bool = false

// SKU parameters
@description('Azure OpenAI SKU')
param openAiSku string = 'S0'

@description('PostgreSQL SKU name')
param postgresSku string = 'Standard_B2s'

@description('PostgreSQL SKU tier')
param postgresSkuTier string = 'Burstable'

@description('App Service Plan SKU')
param appServicePlanSku string = 'B2'

@description('Storage Account SKU')
param storageSku string = 'Standard_LRS'

@description('Document Intelligence SKU')
param docIntelligenceSku string = 'S0'

// Variables
var resourceSuffix = '${baseName}-${environment}'
var resourceSuffixClean = replace(resourceSuffix, '-', '')

var tags = {
  Environment: environment
  Project: 'RAG Agent API'
  ManagedBy: 'Bicep'
  DeployedAt: utcNow('yyyy-MM-dd')
}

// ============================================================================
// MODULE DEPLOYMENTS
// ============================================================================

// Azure OpenAI
module openAi 'modules/openai.bicep' = {
  name: 'openai-deployment'
  params: {
    name: 'oai-${resourceSuffix}'
    location: location
    sku: openAiSku
    tags: tags
  }
}

// PostgreSQL with pgvector
module postgres 'modules/postgres.bicep' = {
  name: 'postgres-deployment'
  params: {
    name: 'psql-${resourceSuffix}'
    location: location
    administratorLogin: 'ragadmin'
    administratorPassword: postgresAdminPassword
    skuName: postgresSku
    skuTier: postgresSkuTier
    databaseName: 'ragagentdb'
    tags: tags
  }
}

// Application Insights
module insights 'modules/insights.bicep' = {
  name: 'insights-deployment'
  params: {
    name: 'appi-${resourceSuffix}'
    logAnalyticsName: 'log-${resourceSuffix}'
    location: location
    tags: tags
  }
}

// Storage Account
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    name: 'st${resourceSuffixClean}'
    location: location
    sku: storageSku
    tags: tags
  }
}

// Azure Communication Services
module acs 'modules/acs.bicep' = {
  name: 'acs-deployment'
  params: {
    name: 'acs-${resourceSuffix}'
    emailDomainPrefix: resourceSuffix
    tags: tags
  }
}

// Key Vault
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  params: {
    name: 'kv-${resourceSuffix}'
    location: location
    tags: tags
    openAiApiKey: openAi.outputs.apiKey
    postgresConnectionString: postgres.outputs.connectionString
    storageConnectionString: storage.outputs.connectionString
    acsConnectionString: acs.outputs.connectionString
    appInsightsConnectionString: insights.outputs.connectionString
  }
  dependsOn: [
    openAi
    postgres
    storage
    acs
    insights
  ]
}

// App Service (API + UI)
module app 'modules/app.bicep' = {
  name: 'app-deployment'
  params: {
    name: 'app-${resourceSuffix}'
    appServicePlanName: 'asp-${resourceSuffix}'
    location: location
    sku: appServicePlanSku
    tags: tags
    openAiEndpoint: openAi.outputs.endpoint
    openAiApiKey: openAi.outputs.apiKey
    postgresConnectionString: postgres.outputs.connectionString
    storageConnectionString: storage.outputs.connectionString
    acsConnectionString: acs.outputs.connectionString
    appInsightsConnectionString: insights.outputs.connectionString
    keyVaultUri: keyVault.outputs.uri
  }
  dependsOn: [
    openAi
    postgres
    storage
    acs
    insights
    keyVault
  ]
}

// Document Intelligence (optional)
module docIntelligence 'modules/docintelligence.bicep' = if (enableDocumentIntelligence) {
  name: 'docintelligence-deployment'
  params: {
    name: 'di-${resourceSuffix}'
    location: location
    sku: docIntelligenceSku
    tags: tags
  }
}

// ============================================================================
// ROLE ASSIGNMENTS - Key Vault access for App Services
// ============================================================================

// Key Vault Secrets User role for API
resource apiKeyVaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.outputs.id, app.outputs.apiPrincipalId, 'Key Vault Secrets User')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: app.outputs.apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User role for UI
resource uiKeyVaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.outputs.id, app.outputs.uiPrincipalId, 'Key Vault Secrets User')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: app.outputs.uiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================================
// OUTPUTS
// ============================================================================

// OpenAI
@description('Azure OpenAI endpoint')
output openAiEndpoint string = openAi.outputs.endpoint

@description('Azure OpenAI GPT deployment name')
output openAiGptDeployment string = openAi.outputs.gptDeploymentName

@description('Azure OpenAI Embedding deployment name')
output openAiEmbeddingDeployment string = openAi.outputs.embeddingDeploymentName

// PostgreSQL
@description('PostgreSQL server FQDN')
output postgresServerFqdn string = postgres.outputs.fqdn

@description('PostgreSQL database name')
output postgresDatabaseName string = postgres.outputs.databaseName

@description('PostgreSQL connection string (sensitive)')
#disable-next-line outputs-should-not-contain-secrets
output postgresConnectionString string = postgres.outputs.connectionString

// Storage
@description('Storage account name')
output storageAccountName string = storage.outputs.name

@description('Storage blob endpoint')
output storageBlobEndpoint string = storage.outputs.primaryEndpoint

@description('Storage connection string (sensitive)')
#disable-next-line outputs-should-not-contain-secrets
output storageConnectionString string = storage.outputs.connectionString

// ACS
@description('Azure Communication Services email domain')
output acsEmailDomain string = acs.outputs.emailDomain

@description('Azure Communication Services endpoint')
output acsEndpoint string = acs.outputs.endpoint

// Key Vault
@description('Key Vault name')
output keyVaultName string = keyVault.outputs.name

@description('Key Vault URI')
output keyVaultUri string = keyVault.outputs.uri

// Application Insights
@description('Application Insights name')
output appInsightsName string = insights.outputs.name

@description('Application Insights instrumentation key')
output appInsightsInstrumentationKey string = insights.outputs.instrumentationKey

// App Service
@description('API URL')
output apiUrl string = app.outputs.apiUrl

@description('UI URL')
output uiUrl string = app.outputs.uiUrl

@description('API Web App name')
output apiAppName string = app.outputs.apiName

@description('UI Web App name')
output uiAppName string = app.outputs.uiName

// Document Intelligence (conditional)
@description('Document Intelligence endpoint')
output docIntelligenceEndpoint string = enableDocumentIntelligence ? docIntelligence.outputs.endpoint : 'Not deployed'
