// Azure OpenAI Service module
// Deploys Azure OpenAI with GPT-35-Turbo and text-embedding-ada-002 models

@description('Name of the Azure OpenAI resource')
param name string

@description('Location for the resource')
param location string

@description('SKU for Azure OpenAI')
@allowed(['S0'])
param sku string = 'S0'

@description('Tags to apply to the resource')
param tags object = {}

// Azure OpenAI Account
resource openAiAccount 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: name
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: sku
  }
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// GPT-35-Turbo deployment for chat completions
resource gptDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openAiAccount
  name: 'gpt-35-turbo'
  sku: {
    name: 'Standard'
    capacity: 30
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-35-turbo'
      version: '0613'
    }
    raiPolicyName: 'Microsoft.Default'
  }
}

// Text-embedding-ada-002 deployment for embeddings
resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openAiAccount
  name: 'text-embedding-ada-002'
  sku: {
    name: 'Standard'
    capacity: 30
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-ada-002'
      version: '2'
    }
    raiPolicyName: 'Microsoft.Default'
  }
  dependsOn: [gptDeployment] // Sequential deployment to avoid conflicts
}

// Outputs
@description('Azure OpenAI endpoint')
output endpoint string = openAiAccount.properties.endpoint

@description('Azure OpenAI resource ID')
output id string = openAiAccount.id

@description('Azure OpenAI resource name')
output name string = openAiAccount.name

@description('GPT deployment name')
output gptDeploymentName string = gptDeployment.name

@description('Embedding deployment name')
output embeddingDeploymentName string = embeddingDeployment.name

@description('Azure OpenAI API key')
output apiKey string = openAiAccount.listKeys().key1
