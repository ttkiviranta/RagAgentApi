// Production environment parameters
using 'main.bicep'

param environment = 'prod'
param location = 'swedencentral'
param baseName = 'ragagent'

// Use secure parameter file or Azure Key Vault reference for password
param postgresAdminPassword = '' // Set via --parameters or secure file

// SKUs - Production-grade
param openAiSku = 'S0'
param postgresSku = 'Standard_D2s_v3'
param postgresSkuTier = 'GeneralPurpose'
param appServicePlanSku = 'P1v3'
param storageSku = 'Standard_GRS'

// Optional features
param enableDocumentIntelligence = true
param docIntelligenceSku = 'S0'
