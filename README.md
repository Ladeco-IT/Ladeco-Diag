# Ladeco Diag

Professionele Windows 11 diagnostische desktopapplicatie voor Ladeco IT.

## Stack

- .NET 9
- WPF (MVVM)
- CommunityToolkit.Mvvm
- Serilog
- LiveCharts2
- QuestPDF
- EF Core Sqlite

## Solution

- src/Ladeco.Diag.App: WPF UI + host bootstrap
- src/Ladeco.Diag.Application: use-cases, orchestratie, contracts
- src/Ladeco.Diag.Domain: domainmodellen en diagnose-entiteiten
- src/Ladeco.Diag.Infrastructure: Windows collectors en systeemdiagnostiek
- src/Ladeco.Diag.Reporting: PDF/HTML/JSON/CSV exports
- tests/Ladeco.Diag.Tests: unit tests voor kritieke componenten

## Build

Vereist Windows + .NET SDK 9.

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Ladeco.Diag.App
```

## Installer Maken (Windows)

Benodigd:

- .NET SDK 9
- Inno Setup 6 (ISCC.exe)

Commando:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\create-installer.ps1 -Configuration Release -Runtime win-x64 -Version 1.0.0
```

Output:

- Publish folder: artifacts/publish/win-x64
- Installer: artifacts/installer/LadecoDiagSetup_1.0.0.exe

Optioneel zonder self-contained runtime:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\create-installer.ps1 -Configuration Release -Runtime win-x64 -Version 1.0.0 -SelfContained:$false
```

## Reeds Geimplementeerd

- Dashboard met kernsysteeminformatie, resource-usage en statusvelden
- Diagnosemodules: system health, internet, security, hardware inventory, SMART, temperatuur, printers, Office, browsers, software inventory, drivers, Windows integriteit
- Herstelacties met bevestiging voor support-operaties (DNS, netwerk, DISM/SFC/CHKDSK, spooler, explorer, cleanup, reboot)
- Rapportexport naar PDF, JSON, CSV en HTML
- Lokale scanhistoriek via EF Core Sqlite
- AI-uitlegblok dat per topfinding oorzaak, risico en oplossingspad samenvat
