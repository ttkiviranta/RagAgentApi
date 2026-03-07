# Error Notification Configuration Guide

## 📧 Email Notifications

### Status: ✅ WORKING (Azure Communication Services)

The application supports two email providers:
1. **Azure Communication Services** (Primary - Production Ready)
2. **SendGrid** (Fallback provider)

---

## Azure Communication Services Setup

### Prerequisites
- Azure Communication Services resource created
- Verified email address or domain linked

### Configuration in appsettings.Development.json

```json
"EmailSettings": {
  "Provider": "AzureCommunicationServices",
  "AzureCommunicationServicesConnectionString": "endpoint=https://YOUR-RESOURCE.communication.azure.com/;accesskey=YOUR_ACCESS_KEY",
  "FromAddress": "your-verified-email@azurecomm.net",
  "FromName": "RAG Agent Alerts",
  "SendGridApiKey": "SG.xxxxx..." // Fallback only
}
```

### Verify Email Configuration
```powershell
# Test email sending
curl -X POST 'https://localhost:7000/api/Test/send-test-email?email=your-email@example.com'
# Expected response: {"message": "Test email sent successfully"}
```

### Known Requirements
⚠️ **Important**: From address must be **verified/linked** in Azure Communication Services  
- Visit Azure Portal → Communication Services → Email → Email domains
- Verify ownership of sender domain or use verified email address

---

## SendGrid Fallback Setup

If Azure CS is unavailable, change provider:

```json
"EmailSettings": {
  "Provider": "SendGrid",
  "SendGridApiKey": "SG.xxxxxxxxxxxxx",
  "FromAddress": "noreply@yourdomain.com",
  "FromName": "RAG Agent Alerts"
}
```

Get API key: https://app.sendgrid.com/settings/api_keys

---

## Teams & Slack Notifications

### Microsoft Teams

```json
"NotificationConfigs": [
  {
    "Id": "teams-dev",
    "Channel": "teams",
    "Enabled": true,
    "WebhookUrl": "https://outlook.webhook.office.com/webhookb2/YOUR_WEBHOOK_URL",
    "Description": "Send alerts to Teams development channel"
  }
]
```

**Setup**: Teams → Apps → Incoming Webhook

### Slack

```json
"NotificationConfigs": [
  {
    "Id": "slack-alerts",
    "Channel": "slack",
    "Enabled": true,
    "WebhookUrl": "https://hooks.slack.com/services/YOUR_WEBHOOK_URL",
    "Description": "Send alerts to Slack channel"
  }
]
```

**Setup**: Slack → Apps → Incoming Webhooks

---

## Test Notifications

Use ErrorDashboard or API endpoint:

```bash
# Send test email
POST /api/test/send-test-email?email=test@example.com

# Log test error
POST /api/error-logging-test/log-test-error

# View errors
GET /api/error-logging-test/errors?limit=50&offset=0
```

---

## Troubleshooting

### "DomainNotLinked" Error
❌ **Problem**: Sender domain not verified in Azure CS  
✅ **Solution**: Verify domain in Azure Portal or use verified email address

### Email not sent
- Check appsettings.Development.json for correct connection string
- Verify from address matches configured domain
- Check Server logs for detailed error messages
- Application Insights → Logs → filter by "email"

## 💬 Microsoft Teams Notifications

### Vaihe 1: Luo Teams Incoming Webhook

1. Avaa Microsoft Teams
2. Mene kanavalle mihin haluat notifikaatiot
3. Valitse **⋯ (More options)** → **Connectors**
4. Etsi **Incoming Webhook**
5. Valitse **Configure**
6. Anna nimi esim. "RAG Agent Alerts"
7. Klikkaa **Create**
8. **Kopioi Webhook URL**

### Vaihe 2: Päivitä appsettings.Development.json

```json
"NotificationConfigs": [
  {
    "Id": "teams-dev",
    "Channel": "teams",
    "Enabled": true,
    "WebhookUrl": "https://outlook.webhook.office.com/webhookb2/...",
    "Description": "Send alerts to Teams development channel"
  }
]
```

### Vaihe 3: Testaa

Kun virhe tapahtuu, Teams-kanava saa viestin automaattisesti.

---

## 📨 Slack Notifications

### Vaihe 1: Luo Slack Incoming Webhook

1. Avaa https://api.slack.com/
2. Valitse oma workspace
3. Valitse **Create New App** → **From scratch**
4. Anna app-nimeksi "RAG Agent"
5. Valitse workspace
6. Mene **Incoming Webhooks** → **Add New Webhook to Workspace**
7. Valitse kanava (esim. #alerts)
8. **Kopioi Webhook URL**

### Vaihe 2: Päivitä appsettings.Development.json

```json
"NotificationConfigs": [
  {
    "Id": "slack-alerts",
    "Channel": "slack",
    "Enabled": true,
    "WebhookUrl": "https://hooks.slack.com/services/...",
    "Description": "Send alerts to Slack channel"
  }
]
```

### Vaihe 3: Testaa

Kun virhe tapahtuu, Slack-kanava saa viestin automaattisesti.

---

## ✅ Konfiguraation Tarkistus

Varmista että:

1. ✅ NotificationConfigs on lisätty appsettings.Development.json:iin
2. ✅ Vähintään yksi notification on `"Enabled": true`
3. ✅ WebhookUrl tai Recipients on konfiguroitu
4. ✅ EmailSettings on konfiguroitu (jos käytät sähköpostia)
5. ✅ Palvelut (Teams, Slack, Gmail) on valtuutettu

---

## 🧪 Testaaminen

### Oikea virhelähetysten testaus:

Sovelluksessa virhe tallennetaan ja lähetetään konfiguroinneille kanavalle:

```
Virhe tapahtuu
   ↓
ErrorOrchestrator käsittelee
   ↓
NotificationRouter reitittää
   ↓
Email/Teams/Slack vastaanottaa
```

### Manuaalinen testaus:

Käynnistä sovellus ja paina jotain, joka aiheuttaa virheen.

---

## 🔒 Turvallisuus

**MUISTA:**
- ❌ **Älä commitoi** appsettings.Development.json Gittiin (on .gitignore:ssa)
- ❌ **Älä jaaa** Webhook URL:eja tai sähköpostisalasanoja
- ✅ Käytä **App Passwords** Gmailissa (ei pääsalasanaa)
- ✅ Käytä **Environment Variables** tuotannossa

**Production-asetukset:**

Tuotannossa käytä Azure Key Vault tai environment variables:

```powershell
# PowerShell
$env:EmailSettings__Password = "your-password"
$env:NotificationConfigs__0__WebhookUrl = "your-webhook"
```

---

## 🆘 Ongelmien ratkaiseminen

### "Email failed to send"
- ✓ Tarkista App Password (ei pääsalasana)
- ✓ Tarkista SmtpServer osoite
- ✓ Tarkista portti (587 TLS)
- ✓ Tarkista internet-yhteys

### "Teams webhook returned 401"
- ✓ Tarkista webhook URL
- ✓ Luo uusi webhook Teams-kanavalla
- ✓ Tarkista että kanava vielä olemassa

### "Slack notification not received"
- ✓ Tarkista webhook URL
- ✓ Varmista että sovellus on lisätty Slack-workspaceen
- ✓ Luo uusi webhook ja kokeile

---

## 📋 Checklist

- [ ] Email konfiguroitu ja testattavissa
- [ ] Teams webhook olemassa
- [ ] Slack webhook olemassa
- [ ] Vähintään yksi notification enabled
- [ ] Kaikki WebhookUrl:t ja sähköpostit konfiguroitu
- [ ] Testaus suoritettu onnistuneesti
- [ ] Dokumentaatio päivitetty

Aloita konfiguraatiosta joka sopii sinulle parhaiten! 🚀
