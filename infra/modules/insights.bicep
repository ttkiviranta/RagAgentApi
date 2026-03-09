// Application Insights and Log Analytics module
// Deploys monitoring infrastructure for telemetry

@description('Name of the Application Insights resource')
param name string

@description('Name of the Log Analytics workspace')
param logAnalyticsName string

@description('Location for the resources')
param location string

@description('Tags to apply to the resources')
param tags object = {}

@description('Email address for error alerts')
param alertEmailAddress string = ''

@description('Enable email alerts for exceptions')
param enableAlerts bool = true

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: 1
    }
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    RetentionInDays: 30
  }
}

// Action Group for email notifications
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (enableAlerts && !empty(alertEmailAddress)) {
  name: 'ag-${name}-errors'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'ErrorAlerts'
    enabled: true
    emailReceivers: [
      {
        name: 'AdminEmail'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ]
  }
}

// Scheduled Query Rule for exception alerts (Log Analytics based)
resource exceptionAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = if (enableAlerts && !empty(alertEmailAddress)) {
  name: 'alert-exceptions-${name}'
  location: location
  tags: tags
  properties: {
    displayName: 'Application Exceptions Alert'
    description: 'Alerts when exceptions occur in UI or API'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    scopes: [
      appInsights.id
    ]
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'exceptions | summarize count() by bin(timestamp, 5m)'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: enableAlerts && !empty(alertEmailAddress) ? [actionGroup.id] : []
    }
    autoMitigate: true
  }
}

// Outputs
@description('Application Insights instrumentation key')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('Application Insights connection string')
output connectionString string = appInsights.properties.ConnectionString

@description('Application Insights resource ID')
output id string = appInsights.id

@description('Application Insights name')
output name string = appInsights.name

@description('Log Analytics workspace ID')
output logAnalyticsId string = logAnalytics.id

@description('Log Analytics workspace name')
output logAnalyticsName string = logAnalytics.name

@description('Action Group ID')
output actionGroupId string = enableAlerts && !empty(alertEmailAddress) ? actionGroup.id : ''
