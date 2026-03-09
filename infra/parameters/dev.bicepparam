// Development environment parameters
using 'main.bicep'

param environment = 'dev'
param location = 'swedencentral'
param baseName = 'ragagent'

// Use secure parameter file or Azure Key Vault reference for password
param postgresAdminPassword = '' // Set via --parameters or secure file

// SKUs - Cost-optimized for development
param openAiSku = 'S0'
param postgresSku = 'Standard_B1ms'
param postgresSkuTier = 'Burstable'
param appServicePlanSku = 'B1'
param storageSku = 'Standard_LRS'

// Optional features
param enableDocumentIntelligence = false
