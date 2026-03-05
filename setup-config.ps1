#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Asetustiedostojen konfigurointiskripti paikalliseen kehitykseen
.DESCRIPTION
    Tämä skripti auttaa kehittäjiä luomaan oikeat appsettings-tiedostot
    ja asettamaan Azure-arvot turvallisesti.
#>

Write-Host "=== RagAgentApi Konfigurointiskripti ===" -ForegroundColor Green
Write-Host ""

# Tarkista onko appsettings.Development.json olemassa
if (Test-Path "appsettings.Development.json") {
    Write-Host "✓ appsettings.Development.json on olemassa" -ForegroundColor Green
} else {
    Write-Host "✗ appsettings.Development.json puuttuu" -ForegroundColor Red
    exit 1
}

# Kysy Azure-asetukset
Write-Host ""
Write-Host "Lisää Azure OpenAI -asetukset:" -ForegroundColor Cyan
$openaiEndpoint = Read-Host "Azure OpenAI Endpoint (esim. https://your-resource.openai.azure.com/)"
$openaiKey = Read-Host "Azure OpenAI Key" -AsSecureString

Write-Host ""
Write-Host "Lisää Azure Search -asetukset:" -ForegroundColor Cyan
$searchEndpoint = Read-Host "Azure Search Endpoint (esim. https://your-search.search.windows.net/)"
$searchKey = Read-Host "Azure Search Key" -AsSecureString

Write-Host ""
Write-Host "Lisää Azure Storage -asetukset:" -ForegroundColor Cyan
$storageConnection = Read-Host "Storage Connection String" -AsSecureString

Write-Host ""
Write-Host "Lisää PostgreSQL -asetukset:" -ForegroundColor Cyan
$postgresPassword = Read-Host "PostgreSQL Password" -AsSecureString
$postgresHost = Read-Host "PostgreSQL Host (oletus: localhost)" -ErrorAction Continue
if ([string]::IsNullOrEmpty($postgresHost)) { $postgresHost = "localhost" }
$postgresPort = Read-Host "PostgreSQL Port (oletus: 5432)" -ErrorAction Continue
if ([string]::IsNullOrEmpty($postgresPort)) { $postgresPort = "5432" }

# Muunna SecureString -> String
$openaiKeyPlain = [System.Net.NetworkCredential]::new("", $openaiKey).Password
$searchKeyPlain = [System.Net.NetworkCredential]::new("", $searchKey).Password
$storageConnectionPlain = [System.Net.NetworkCredential]::new("", $storageConnection).Password
$postgresPasswordPlain = [System.Net.NetworkCredential]::new("", $postgresPassword).Password

# Lue nykyinen appsettings.Development.json
$config = Get-Content "appsettings.Development.json" | ConvertFrom-Json

# Päivitä arvot
$config.Azure.OpenAI.Endpoint = $openaiEndpoint
$config.Azure.OpenAI.Key = $openaiKeyPlain
$config.Azure.Search.Endpoint = $searchEndpoint
$config.Azure.Search.Key = $searchKeyPlain
$config.Azure.Storage.ConnectionString = $storageConnectionPlain
$config.ConnectionStrings.PostgreSQL = "Host=$postgresHost;Port=$postgresPort;Database=ragagentdb;Username=postgres;Password=$postgresPasswordPlain"
$config.DemoSettings.PostgresConnectionString = "Host=$postgresHost;Port=$postgresPort;Database=ragagentdb;Username=postgres;Password=$postgresPasswordPlain"

# Kirjoita takaisin
$config | ConvertTo-Json -Depth 10 | Set-Content "appsettings.Development.json"

Write-Host ""
Write-Host "✓ Konfiguraatio päivitetty!" -ForegroundColor Green
Write-Host "📝 Tiedostot: appsettings.Development.json ja RagAgentUI/appsettings.Development.json" -ForegroundColor Yellow
Write-Host "⚠️  MUISTA: Älä commitoi näitä tiedostoja Gittiin!" -ForegroundColor Red
Write-Host ""
Write-Host "Voit nyt käynnistää sovelluksen: dotnet run" -ForegroundColor Green
