// Azure Document Intelligence module (optional)
// Deploys Document Intelligence for PDF and document processing

@description('Name of the Document Intelligence resource')
param name string

@description('Location for the resource')
param location string

@description('SKU for Document Intelligence')
@allowed(['F0', 'S0'])
param sku string = 'S0'

@description('Tags to apply to the resource')
param tags object = {}

// Document Intelligence (Form Recognizer)
resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: name
  location: location
  tags: tags
  kind: 'FormRecognizer'
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

// Outputs
@description('Document Intelligence endpoint')
output endpoint string = documentIntelligence.properties.endpoint

@description('Document Intelligence resource ID')
output id string = documentIntelligence.id

@description('Document Intelligence name')
output name string = documentIntelligence.name

@description('Document Intelligence API key')
output apiKey string = documentIntelligence.listKeys().key1
