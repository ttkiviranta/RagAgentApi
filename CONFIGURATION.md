# Konfiguraatio (appsettings)

## 📋 Tiedostojen rakenne

```
appsettings.json                        → Paikallinen kehitys (älä commitoi!)
appsettings.json.template               → Template-pohja (versionhallinnassa)
appsettings.Development.json            → Development-asetukset (älä commitoi!)
appsettings.Development.json.template   → Development-template (versionhallinnassa)
RagAgentUI/appsettings.json             → UI:n asetukset (älä commitoi!)
RagAgentUI/appsettings.Development.json → UI:n dev-asetukset (älä commitoi!)
```

## 🔒 Tietoturva

**KAIKKI** seuraavat tiedostot sisältävät salasanoja ja API-avaimia:
- ❌ `appsettings.json`
- ❌ `appsettings.Development.json`
- ❌ `RagAgentUI/appsettings.*.json`

**NÄMÄ** tiedostot ovat turvassa (template-pohjat):
- ✅ `appsettings.*.template`

`.gitignore` estää niiden commitoinnin automaattisesti.

## 🚀 Ensimmäinen käyttöönotto

### Vaihtoehto 1: Skriptin käyttö (suositeltu)

```powershell
# Suorita konfigurointiskripti
./setup-config.ps1

# Vastaa kehotteisiin:
# - Azure OpenAI Endpoint
# - Azure OpenAI Key
# - Azure Search Endpoint
# - Azure Search Key
# - Storage Connection String
# - PostgreSQL Password
```

### Vaihtoehto 2: Manuaalinen muokkaus

1. Kopioi template-tiedostot:
   ```bash
   cp appsettings.json.template appsettings.json
   cp appsettings.Development.json.template appsettings.Development.json
   cp RagAgentUI/appsettings.*.template RagAgentUI/appsettings.*.json
   ```

2. Avaa `appsettings.Development.json` ja korvaa:
   ```json
   {
     "Azure": {
       "OpenAI": {
         "Endpoint": "https://YOUR_RESOURCE.openai.azure.com/",
         "Key": "YOUR_ACTUAL_KEY_HERE",
         ...
       }
     }
   }
   ```

## 📋 Vaaditut Azure-resurssit

Seuraavat on luotava Azure Portalissa:

### 1. Azure OpenAI Service
- **Endpoint**: `https://[resource-name].openai.azure.com/`
- **API Key**: Löytyy "Keys and Endpoint" -osiosta
- **Deployments**:
  - `text-embedding-ada-002` (embeddings)
  - `gpt-35-turbo` (chat completions)

### 2. Azure Cognitive Search
- **Endpoint**: `https://[resource-name].search.windows.net/`
- **API Key**: Admin key tai Query key

### 3. Azure Storage Account
- **Connection String**: Löytyy "Access Keys" -osiosta
  ```
  DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net
  ```

### 4. Azure Document Intelligence (valinnainen)
- **Endpoint**: `https://[resource-name].cognitiveservices.azure.com/`
- **API Key**: Löytyy "Keys and Endpoints" -osiosta

## 🗄️ PostgreSQL

Kehityksessä käytetään paikallista PostgreSQL-instanssia:

```bash
# Docker-kontissa (suositeltava)
docker run -d \
  --name postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=ragagentdb \
  -p 5432:5432 \
  postgres:15

# Tarkista yhteys
psql -h localhost -U postgres -d ragagentdb
```

## ✅ Tarkistus

Varmista että nämä tiedostot **EIVÄT OLE** Git Stagingissa:

```bash
git status

# Näytä vain appsettings-tiedostot
git status | grep appsettings
```

Pitäisi näyttää:
```
not staged for commit:
    modified:   appsettings.json
    modified:   appsettings.Development.json
    modified:   RagAgentUI/appsettings.Development.json
```

Jos näet ne `untracked files` -osiossa, `.gitignore` on epäonnistunut ⚠️

## 🔄 Ympäristön vaihtaminen

```bash
# Development (oletus)
ASPNETCORE_ENVIRONMENT=Development dotnet run

# Production
ASPNETCORE_ENVIRONMENT=Production dotnet run
```

## ⚠️ Varmuuskopiot

Säilytä turvalliset kopiot näistä tiedostoista:
- 💾 Paikallisella koneella (älä commitoi!)
- 🔐 Salasanojen hallintajärjestelmässä
- ☁️ Vain production-avaimet Azure Key Vault:issa

## 🐛 Ongelmien ratkaiseminen

### "Connection refused" PostgreSQL-yhteydessä
```bash
# Tarkista PostgreSQL-kontille
docker ps | grep postgres

# Käynnistä PostgreSQL
docker run -d --name postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:15
```

### "401 Unauthorized" Azure API:n kanssa
- ✓ Tarkista API-avain `appsettings.Development.json`-tiedostosta
- ✓ Varmista että resurssi on "Enabled" Azure Portalissa
- ✓ Tarkista että endpoint on oikea muoto: `https://[name].openai.azure.com/`

### "Key not valid for this geolocation"
- Valitse Azure-resurssit samalta alueelta
- Varmista että avaimet eivät ole vanhentuneet

