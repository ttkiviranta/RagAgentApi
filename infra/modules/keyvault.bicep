// Azure Key Vault module
// Deploys Key Vault for secrets management

@description('Name of the Key Vault')
param name string

@description('Location for the resource')
param location string

@description('Tenant ID for Azure AD')
param tenantId string = subscription().tenantId

@description('Object ID of the principal that should have access')
param principalId string = ''

@description('Enable soft delete')
param enableSoftDelete bool = true

@description('Soft delete retention in days')
param softDeleteRetentionInDays int = 7

@description('Tags to apply to the resource')
param tags object = {}

// Secrets to store
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
@secure()
param appInsightsConnectionString string = ''

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    enabledForDeployment: true
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: enableSoftDelete
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enableRbacAuthorization: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Store secrets if provided
resource openAiSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(openAiApiKey)) {
  parent: keyVault
  name: 'OpenAI-ApiKey'
  properties: {
    value: openAiApiKey
    contentType: 'text/plain'
  }
}

resource postgresSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(postgresConnectionString)) {
  parent: keyVault
  name: 'PostgreSQL-ConnectionString'
  properties: {
    value: postgresConnectionString
    contentType: 'text/plain'
  }
}

resource storageSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(storageConnectionString)) {
  parent: keyVault
  name: 'Storage-ConnectionString'
  properties: {
    value: storageConnectionString
    contentType: 'text/plain'
  }
}

resource acsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(acsConnectionString)) {
  parent: keyVault
  name: 'ACS-ConnectionString'
  properties: {
    value: acsConnectionString
    contentType: 'text/plain'
  }
}

resource appInsightsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(appInsightsConnectionString)) {
  parent: keyVault
  name: 'AppInsights-ConnectionString'
  properties: {
    value: appInsightsConnectionString
    contentType: 'text/plain'
  }
}

// Outputs
@description('Key Vault name')
output name string = keyVault.name

@description('Key Vault ID')
output id string = keyVault.id

@description('Key Vault URI')
output uri string = keyVault.properties.vaultUri
