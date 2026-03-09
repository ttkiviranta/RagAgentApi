// Azure Communication Services module
// Deploys ACS for email notifications

@description('Name of the Communication Services resource')
param name string

@description('Location for the resource (global for ACS)')
param location string = 'global'

@description('Email domain name prefix')
param emailDomainPrefix string

@description('Tags to apply to the resource')
param tags object = {}

// Communication Services
resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    dataLocation: 'Europe'
  }
}

// Email Communication Service
resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: '${name}-email'
  location: location
  tags: tags
  properties: {
    dataLocation: 'Europe'
  }
}

// Azure Managed Email Domain
resource emailDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: location
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

// Link email service to communication service
resource linkEmailToAcs 'Microsoft.Communication/communicationServices/emailServices@2023-04-01' = {
  parent: communicationService
  name: emailService.name
}

// Outputs
@description('Communication Services endpoint')
output endpoint string = communicationService.properties.hostName

@description('Communication Services resource ID')
output id string = communicationService.id

@description('Communication Services name')
output name string = communicationService.name

@description('Email domain')
output emailDomain string = emailDomain.properties.fromSenderDomain

@description('Communication Services connection string')
output connectionString string = communicationService.listKeys().primaryConnectionString

@description('Email service name')
output emailServiceName string = emailService.name
