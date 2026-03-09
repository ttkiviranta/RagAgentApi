# Application Insights - Virhe-ilmoitukset sähköpostiin

Tämä dokumentaatio kuvaa miten UI- ja API-virheistä saa sähköposti-ilmoitukset Azure Application Insightsin kautta.

## 📊 Arkkitehtuuri

```
┌─────────────────┐     ┌─────────────────┐
│   RagAgentUI    │     │   RagAgentApi   │
│  (Blazor Server)│     │   (ASP.NET)     │
└────────┬────────┘     └────────┬────────┘
         │                       │
         │  TelemetryClient      │  TelemetryClient
         │                       │
         └───────────┬───────────┘
                     │
                     ▼
         ┌───────────────────────┐
         │  Application Insights │
         │                       │
         │  ┌─────────────────┐  │
         │  │   Alert Rules   │  │
         │  │  (exceptions)   │  │
         │  └────────┬────────┘  │
         └───────────┼───────────┘
                     │
                     ▼
         ┌───────────────────────┐
         │    Action Group       │
         │  ┌─────────────────┐  │
         │  │  Email Action   │──┼──► admin@example.com
         │  └─────────────────┘  │
         └───────────────────────┘
```

## 🛠️ Asennus

### 1. Application Insights -resurssi

Application Insights luodaan Bicep-mallilla (`infra/modules/insights.bicep`).

### 2. Connection String konfiguraatio

**API (`appsettings.json`):**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=xxx;IngestionEndpoint=https://..."
  }
}
```

**UI (`appsettings.json`):**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=xxx;IngestionEndpoint=https://..."
  }
}
```

### 3. Azure Alert Rule luonti

#### Vaihtoehto A: Azure Portal

1. Avaa **Application Insights** → **Alerts** → **+ Create** → **Alert rule**
2. **Condition**: 
   - Signal: `Exceptions`
   - Threshold: `Greater than 0`
   - Evaluation: Every 5 minutes
3. **Actions**:
   - Create new Action Group
   - Add Email action
4. **Details**:
   - Name: `UI-API-Error-Alert`
   - Severity: `2 - Warning`

#### Vaihtoehto B: Bicep-malli

Lisää `infra/modules/insights.bicep` tiedostoon:

```bicep
// Alert rule for exceptions
resource exceptionAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-exceptions-${name}'
  location: 'global'
  properties: {
    description: 'Alert when exceptions occur in UI or API'
    severity: 2
    enabled: true
    scopes: [appInsights.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ExceptionCount'
          metricName: 'exceptions/count'
          operator: 'GreaterThan'
          threshold: 0
          timeAggregation: 'Count'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// Action group for email notifications
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${name}-email'
  location: 'global'
  properties: {
    groupShortName: 'ErrorAlert'
    enabled: true
    emailReceivers: [
      {
        name: 'AdminEmail'
        emailAddress: 'admin@example.com'
        useCommonAlertSchema: true
      }
    ]
  }
}
```

## 📧 Sähköposti-ilmoituksen sisältö

Azure Alert -sähköposti sisältää:

| Kenttä | Esimerkki |
|--------|-----------|
| Alert name | UI-API-Error-Alert |
| Severity | Warning |
| Resource | appi-ragagent-dev |
| Timestamp | 2024-01-15 10:30:00 UTC |
| Exception count | 3 |
| Link to portal | [View in Azure Portal] |

## 🔍 Virheiden tarkastelu

### Application Insights Portal

1. **Failures** → Näyttää kaikki virheet
2. **Exceptions** → Yksityiskohtaiset poikkeukset
3. **Transaction search** → Hae tietyillä kriteereillä

### KQL-kyselyt (Kusto)

```kql
// Kaikki UI-virheet viimeisen tunnin aikana
exceptions
| where timestamp > ago(1h)
| where customDimensions.Source startswith "BlazorUI"
| project timestamp, type, outerMessage, customDimensions.Source

// API ja UI virheet yhteensä
exceptions
| where timestamp > ago(24h)
| summarize count() by bin(timestamp, 1h), tostring(customDimensions.Source)
| render timechart
```

## ✅ Testatut virhetyypit

| Virhetyyppi | Lähde | Seuranta |
|-------------|-------|----------|
| Unhandled exception | UI Component | ✅ ErrorBoundary |
| API call failure | HttpClient | ✅ TelemetryClient |
| SignalR disconnect | ChatHubService | ✅ OnError handler |
| Validation error | Form components | ✅ Manual tracking |
| Database error | EF Core | ✅ Backend logging |

## 🧪 Testaus

### Manuaalinen virheen triggeröinti

Lisää testikomponenttiin:

```razor
@inject TelemetryClient TelemetryClient

<button @onclick="ThrowTestError">Trigger Test Error</button>

@code {
    private void ThrowTestError()
    {
        try
        {
            throw new InvalidOperationException("Test error from UI");
        }
        catch (Exception ex)
        {
            TelemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                { "Source", "TestComponent" },
                { "Action", "ManualTest" }
            });
            TelemetryClient.Flush();
        }
    }
}
```

### Tarkista Application Insightsista

1. Odota 2-5 minuuttia (telemetrian viive)
2. Avaa **Application Insights** → **Failures** → **Exceptions**
3. Etsi "Test error from UI"

## 📝 Huomioita

- **Viive**: Application Insights -data voi viivästyä 2-5 minuuttia
- **Sampling**: Tuotannossa voidaan käyttää näytteenottoa kuormituksen vähentämiseksi
- **Kustannukset**: Seuraa Application Insights -datamääriä Azure Cost Managementissa
- **GDPR**: Älä lähetä henkilötietoja telemetriassa
