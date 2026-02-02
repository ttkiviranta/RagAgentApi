# Azure Setup Guide - RAG Agent API

Tämä dokumentaatio opastaa sinua Azure-palveluiden automatisoituun asentamiseen PowerShell-skriptillä.

## ?? Vaatimukset

### Ennen kuin aloitat
1. **Azure Subscription** - Aktiivinen Azure-tili
2. **Azure CLI** - Asenna osoitteesta: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
3. **PowerShell 7+** - Desktoppiversio tai PowerShell Core
4. **Riittävät oikeudet** - Pystyä luomaan resursseja Azure-tilauksessa

### Tarkistus
```powershell
# Tarkista Azure CLI
az --version

# Tarkista PowerShell
$PSVersionTable.PSVersion
```

---

## ?? Pikaopas

### 1. Hanki tarvittavat tiedot
```powershell
# Listaa käytettävissä olevat abonnementit
az account list --output table

# Valitse haluamasi abonnementti
$SubscriptionId = "your-subscription-id-here"

# Listaa saatavilla olevat sijainnit
az account list-locations --output table
```

### 2. Suorita asennus
```powershell
# Siirry projektihakemistoon
cd C:\Users\ttkiv\source\repos\RagAgentApi

# Suorita asennus
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-rg" `
    -Location "westeurope" `
    -EnvironmentName "dev"
```

---

## ?? Skriptin Parametrit

| Parametri | Tyyppi | Pakollinen | Kuvaus |
|-----------|--------|-----------|--------|
| `SubscriptionId` | string | ? | Azure-tilauksesi tunnus |
| `ResourceGroupName` | string | ? | Uuden resurssiryhmän nimi |
| `Location` | string | ? | Azure-alue (esim. westeurope, eastus) |
| `EnvironmentName` | string | | Ympäristö-tunnus (dev, staging, prod) |
| `SkipAISearch` | switch | | Ohita Azure AI Search -luonti |
| `SkipBlobStorage` | switch | | Ohita Azure Blob Storage -luonti |
| `SkipDocumentIntelligence` | switch | | Ohita Document Intelligence -luonti |
| `SkipApplicationInsights` | switch | | Ohita Application Insights -luonti |
| `DryRun` | switch | | Simuloi ilman oikeita muutoksia |

---

## ?? Käyttöesimerkit

### Esimerkki 1: Perusasennus (vain Azure OpenAI)
```powershell
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-dev" `
    -Location "westeurope"
```

### Esimerkki 2: Kaikki palvelut (suositeltu tuotannolle)
```powershell
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-prod" `
    -Location "westeurope" `
    -EnvironmentName "prod"
```

### Esimerkki 3: Testaus ilman muutoksia
```powershell
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-test" `
    -Location "westeurope" `
    -DryRun
```

### Esimerkki 4: Minimaaliset palvelut
```powershell
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-minimal" `
    -Location "westeurope" `
    -SkipAISearch `
    -SkipBlobStorage `
    -SkipDocumentIntelligence `
    -SkipApplicationInsights
```

---

## ?? Palvelut ja niiden roolit

### Pakollinen ?
- **Azure OpenAI Service** - Text embeddings ja chat completions

### Valinnainen (suositeltu)
- **Azure AI Search** - Vektori- ja täyteksthaku (legacy)
- **Azure Blob Storage** - Dokumenttien tallennus
- **Application Insights** - Loki- ja telemetrian seuranta

### Valinnainen
- **Document Intelligence** - Dokumentin tunnistus ja jäsentäminen

---

## ?? Skriptin tuloste

Skripti luo tiedoston `azure-config-{environment}.env` sisältäen:
```
AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.com/
AZURE_OPENAI_KEY=your-key-here
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=text-embedding-ada-002
AZURE_OPENAI_CHAT_DEPLOYMENT=gpt-35-turbo
...
```

---

## ? Konfiguraation viimeistely

### 1. Kopioi arvot appsettings-tiedostoihin

**appsettings.json**
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5433;Database=ragagentdb;Username=postgres;Password=YourPassword"
  },
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://your-openai.openai.azure.com/",
      "Key": "your-openai-key",
      "EmbeddingDeployment": "text-embedding-ada-002",
      "ChatDeployment": "gpt-35-turbo"
    },
    "Search": {
      "Endpoint": "https://your-search.search.windows.net",
      "Key": "your-search-key"
    },
    "Storage": {
      "AccountName": "yourstorageaccount",
      "AccountKey": "your-storage-key"
    },
    "DocumentIntelligence": {
      "Endpoint": "https://your-docint.cognitiveservices.azure.com/",
      "Key": "your-docint-key"
    },
    "ApplicationInsights": {
      "InstrumentationKey": "your-appinsights-key"
    }
  }
}
```

### 2. Suojaa konfiguraatiotiedostot
```powershell
# Lisää .gitignore-tiedostoon
echo "azure-config-*.env" >> .gitignore
echo "appsettings*.json" >> .gitignore

# Älä koskaan commitoi konfiguraatiotiedostoja!
```

### 3. Testaa yhteys
```bash
dotnet run
```

Navigoi osoitteeseen `https://localhost:7000` ja tarkista Swagger UI.

---

## ?? Vianmääritys

### Skripti sanoo: "Azure CLI not installed"
```powershell
# Asenna Azure CLI
# Windows: choco install azure-cli
# Mac: brew install azure-cli
# Linux: curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
```

### Skripti sanoo: "Not authenticated"
```powershell
# Kirjaudu Azure-palveluun
az login

# Jos sinulla on useita tilaajia, valitse oikea
az account set --subscription "your-subscription-id"
```

### Deployment epäonnistuu
```powershell
# Tarkista käytettävissä olevat SKU:t
az cognitiveservices account list-kinds --location westeurope

# Tarkista kiintiöt
az cognitiveservices account list-usage --resource-group rag-agent-rg --name your-openai-name
```

### Palvelua ei löydy Azure-portaalista
```powershell
# Tarkista resurssiryhmä
az group list --output table

# Listaa resurssit resurssiryhmässä
az resource list --resource-group rag-agent-rg --output table
```

---

## ?? Azure-portalissa tarkistaminen

1. Siirry osoitteeseen https://portal.azure.com
2. Etsi resurssiryhmä: `rag-agent-rg` (tai nimesi)
3. Tarkista luodut resurssit:
   - Azure OpenAI Service
   - Azure AI Search (jos ei ohitettu)
   - Storage Account (jos ei ohitettu)
   - Application Insights (jos ei ohitettu)

---

## ?? Resurssin poistaminen (jos tarvitsee)

```powershell
# Poista koko resurssiryhmä (VAROITUS: pysyvä)
az group delete --name rag-agent-rg --yes

# Poista yksittäinen resurssi
az resource delete `
    --resource-group rag-agent-rg `
    --name your-openai-name `
    --resource-type "Microsoft.CognitiveServices/accounts"
```

---

## ?? Parhaita käytäntöjä

? **Tehdä**
- Käytä erilisiä resurssiryhmia dev/staging/prod-ympäristöille
- Säilytä konfiguraatiotiedostot turvallisesti
- Käytä DryRun-vaihtoehtoa ensin testaamisen varalle
- Dokumentoi käytetyt skriptien parametrit

? **Ei saa tehdä**
- Älä commitoi Azure-avaimia Githubiin
- Älä jaa konfiguraatiotiedostoja turvattomasti
- Älä käytä tuotannon avaimia kehityksessä
- Älä poista resurssiryhmää vahingossa

---

## ?? Lisäresurssit

- [Azure OpenAI -dokumentaatio](https://learn.microsoft.com/en-us/azure/cognitive-services/openai/)
- [Azure AI Search -dokumentaatio](https://learn.microsoft.com/en-us/azure/search/)
- [Azure CLI -viite](https://learn.microsoft.com/en-us/cli/azure/)
- [Azure PowerShell -dokumentaatio](https://learn.microsoft.com/en-us/powershell/azure/)

---

## ?? Apua tarvitset?

Jos kohtaat ongelmia:
1. Tarkista lokitiedostot: `$HOME\.azure\cmdline_*_log.txt`
2. Käytä `-Verbose` -vaihtoehtoa lisätiedoille
3. Katso [Azure-tuki](https://azure.microsoft.com/en-us/support/)
