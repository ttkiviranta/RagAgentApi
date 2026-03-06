# SendGrid Email Configuration Guide

## 📧 SendGrid Setup - Step by Step

### Step 1: Create Free SendGrid Account

1. Go to: https://sendgrid.com/
2. Click **"Sign Up"** (Free tier available)
3. Fill in registration form
4. Verify your email address
5. Choose **"Free"** plan (100 emails/day)

### Step 2: Create SendGrid API Key

1. Log in to SendGrid Dashboard
2. Go to **Settings** → **API Keys**
3. Click **"Create API Key"**
4. Choose **"Full Access"** or custom permissions
5. Give it a name: `RAG Agent API`
6. Click **"Create & Copy"**
7. **COPY THE ENTIRE KEY** (shown only once!)

Example format:
```
SG.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

### Step 3: Update appsettings.Development.json

Replace `[YOUR_SENDGRID_API_KEY]` with your actual API key:

```json
"EmailSettings": {
  "Provider": "SendGrid",
  "SendGridApiKey": "SG.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "FromAddress": "ttkiviranta@gmail.com",
  "FromName": "RAG Agent Alerts"
}
```

### Step 4: Enable Email Notifications

```json
"NotificationConfigs": [
  {
    "Id": "email-ops",
    "Channel": "email",
    "Enabled": true,
    "Recipients": [ "ttkiviranta@gmail.com" ],
    "Description": "Send critical errors to operations email"
  }
]
```

---

## ✅ Test Email Sending

### Start Application

```powershell
dotnet run
```

### Send Test Email

**Option A: Using Swagger UI**
1. Open: https://localhost:7000/swagger
2. Find **"Test"** section
3. Click **POST /api/test/send-test-email**
4. Enter email: `ttkiviranta@gmail.com`
5. Click **"Try it out"**

**Option B: Using cURL**

```bash
curl -X POST "https://localhost:7000/api/test/send-test-email?email=ttkiviranta@gmail.com"
```

**Option C: Using PowerShell**

```powershell
$email = "ttkiviranta@gmail.com"
Invoke-WebRequest -Uri "https://localhost:7000/api/test/send-test-email?email=$email" -Method Post
```

### Expected Success Response

```json
{
  "message": "Test email sent successfully",
  "recipient": "ttkiviranta@gmail.com",
  "timestamp": "2026-03-05T12:00:00Z"
}
```

---

## 📊 SendGrid Features

### Free Tier Benefits

- ✅ 100 emails per day
- ✅ Full API access
- ✅ Web UI & API
- ✅ Email templates
- ✅ Activity monitoring
- ✅ Statistics & analytics

### Upgrade When Needed

- **Pro**: $9.95/month = 40,000 emails/month
- **Advanced**: $80/month = 300,000 emails/month
- **Enterprise**: Custom pricing

---

## 🔒 Security Best Practices

### DO:
- ✅ Store API key in `appsettings.Development.json` (local only)
- ✅ Use environment variables for production
- ✅ Rotate API keys periodically
- ✅ Keep API key secret (never commit to Git)

### DON'T:
- ❌ Hardcode API key in code
- ❌ Commit `appsettings.Development.json` with real key
- ❌ Share API key in Slack/email
- ❌ Use same API key for dev and production

### Production Setup

Use environment variables:

```powershell
# PowerShell
$env:EmailSettings__SendGridApiKey = "SG.xxxxx"

# Bash
export EmailSettings__SendGridApiKey="SG.xxxxx"

# Azure Key Vault
az keyvault secret set --vault-name your-vault --name SendGridApiKey --value "SG.xxxxx"
```

---

## 🆘 Troubleshooting

### "API key not configured"
- ✓ Check `appsettings.Development.json`
- ✓ Verify SendGridApiKey is set
- ✓ No spaces or extra characters

### "401 Unauthorized"
- ✓ API key is invalid or expired
- ✓ Create a new API key in SendGrid dashboard
- ✓ Make sure it has email sending permissions

### "Email not received"
- ✓ Check email spam folder
- ✓ Verify recipient email address is correct
- ✓ Check SendGrid dashboard for delivery status
- ✓ FromAddress must be verified in SendGrid

### "Too many requests"
- ✓ Free tier has 100 emails/day limit
- ✓ Upgrade to paid plan if needed
- ✓ Wait until next day for limit reset

---

## 📋 Monitoring

### Check Email Status

1. Go to SendGrid Dashboard
2. Click **"Mail Send"** → **"Statistics"**
3. See delivery metrics:
   - Delivered
   - Opened
   - Clicked
   - Bounced
   - Dropped

### Activity Log

1. Go to **"Mail Activity"** 
2. See all sent emails with status
3. Debug delivery issues

---

## 🚀 Production Deployment

### Azure App Service

```bash
# Set application setting
az webapp config appsettings set \
  --resource-group mygroup \
  --name myapp \
  --settings EmailSettings__SendGridApiKey="SG.xxxxx"
```

### Docker

```dockerfile
ENV EmailSettings__SendGridApiKey=SG.xxxxx
```

### GitHub Actions

```yaml
- name: Deploy
  env:
    SENDGRID_API_KEY: ${{ secrets.SENDGRID_API_KEY }}
  run: dotnet publish
```

---

## ✨ Next Steps

1. ✅ Create SendGrid account (free)
2. ✅ Get API key
3. ✅ Update appsettings.Development.json
4. ✅ Test email sending
5. ✅ Enable email notifications in config
6. ✅ Monitor delivery in SendGrid dashboard

**You're all set! SendGrid is now handling your email notifications.** 🎉
