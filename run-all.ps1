# Start both RagAgentApi and RagAgentUI for local development
# Usage: Open PowerShell as admin (for trusting dev certs) and run: .\run-all.ps1

param()

$solutionRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition

Push-Location $solutionRoot

Write-Host "Starting RagAgentApi..."
Start-Process -FilePath "dotnet" -ArgumentList "run --project RagAgentApi\RagAgentApi.csproj" -NoNewWindow

Start-Sleep -Milliseconds 500

Write-Host "Starting RagAgentUI..."
Start-Process -FilePath "dotnet" -ArgumentList "run --project RagAgentUI\RagAgentUI.csproj" -NoNewWindow

Pop-Location

Write-Host "Both projects started (processes launched). Check their console output or Visual Studio for details."