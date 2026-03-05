# Error Notification Configuration Guide

## 📧 Email Notifications (Gmail/Office 365)

### Vaihe 1: Hanki Gmail App Password

1. Avaa Google Account settings: https://myaccount.google.com
2. Mene kohtaan "Security" (vasemmalla)
3. Ota käyttöön "2-Step Verification" jos ei ole käytössä
4. Valitse "App passwords" (näkyy vain jos 2-Step on käytössä)
5. Valitse sovellukseksi "Mail" ja laitteeksi "Windows Computer"
6. Kopioi generoitu 16-merkkinen salasana

### Vaihe 2: Päivitä appsettings.Development.json

```json
"EmailSettings": {
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "UseTls": true,
  "FromAddress": "your-email@gmail.com",
  "FromName": "RAG Agent Alerts",
  "Username": "your-email@gmail.com",
  "Password": "xxxx xxxx xxxx xxxx"
}
```

### Vaihe 3: Ota Email notifications käyttöön

```json
"NotificationConfigs": [
  {
    "Id": "email-ops",
    "Channel": "email",
    "Enabled": true,
    "Recipients": ["team@company.com", "admin@company.com"],
    "Description": "Send critical errors to operations email"
  }
]
```

**Huomio:** Jos käytät Office 365 / Outlook:
```json
"SmtpServer": "smtp.office365.com",
"SmtpPort": 587,
"Username": "your-email@company.com"
```

---

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
