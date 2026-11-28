# Group Policy Director — Installation (Windows)

Grobes Lab-/On-Prem-Paket: **self-contained win-x64** API (inkl. UI) und Worker, als **MSI** und/oder **ZIP + Setup.ps1**. Intune-Deployment ist bewusst zurückgestellt.

## Voraussetzungen

| Komponente | Hinweis |
|------------|---------|
| Windows Server 2019/2022/2025 oder Windows 10/11 x64 | Zielhost — **kein** Domain Controller als App-Server |
| Administratorrechte | für Dienstinstallation |
| SQL Server 2019+ (oder LocalDB/Express) | Connection String in `appsettings.json` — Persistenz ist noch Stub; Schema unter `database\schema\` |
| Optional: SMTP-Relay | für OOB-Mails |
| Optional: Domänenbeitritt + GPMC | erst nötig für **Live-AD**; ohne Domäne läuft GPD im DryRun |
| .NET Runtime | **nicht** nötig (self-contained) |
| Build-Maschine (nur zum Erzeugen des Pakets) | .NET 9 SDK, Node.js 20+; MSI: WiX Toolset CLI v5+ (`wix`) unter Windows, oder `wixl` (msitools) unter Linux |

## Paket erzeugen

Im Repo-Root (Cloud/Linux oder Windows):

```bash
# Linux / Cloud-Env (ZIP + MSI via wixl)
chmod +x packaging/build.sh
./packaging/build.sh
```

```powershell
# Windows (ZIP + MSI via WiX Toolset CLI `wix`)
# Voraussetzung: winget install WiXToolset.WiXCLI ; wix eula accept wix7
.\packaging\build.ps1
```

Artefakte unter `artifacts/`:

- `GroupPolicyDirector-0.1.0-win-x64.zip` — Payload + Skripte
- `GroupPolicyDirector-0.1.0-win-x64.msi` — Dateiinstallation nach `%ProgramFiles%\Group Policy Director\` (wenn `wix` bzw. `wixl` verfügbar)

## Installation

### Variante A — MSI

```powershell
# UI
msiexec /i GroupPolicyDirector-0.1.0-win-x64.msi

# Still (Silent)
msiexec /i GroupPolicyDirector-0.1.0-win-x64.msi /qn /norestart
```

Anschließend Dienste registrieren (elevated PowerShell):

```powershell
& 'C:\Program Files\Group Policy Director\scripts\Install-Gpd.ps1'
```

### Variante B — ZIP / Setup.exe-Äquivalent

ZIP entpacken, dann:

```powershell
cd .\payload\scripts
.\Setup-Gpd.ps1
# oder: Dateien manuell nach Program Files kopieren und Install-Gpd.ps1 ausführen
```

`Setup-Gpd.ps1` ist das klare Setup ohne MSI: kopiert `Api\` / `Worker\` und legt die Windows-Dienste an.

### Deinstallation der Dienste

```powershell
& 'C:\Program Files\Group Policy Director\scripts\Uninstall-Gpd.ps1'
# MSI zusätzlich: msiexec /x GroupPolicyDirector-0.1.0-win-x64.msi /qn
```

## Konfiguration (Platzhalter)

Nach der Installation (bzw. vor dem ersten Start) `appsettings.json` anpassen:

| Datei | Schlüssel | Zweck |
|-------|-----------|--------|
| `Api\appsettings.json` | `ConnectionStrings:Gpd` | SQL — Platzhalter `REPLACE_SQL_HOST` ersetzen |
| `Api\appsettings.json` | `Smtp:*` | SMTP — `REPLACE_SMTP_HOST`, From, Credentials |
| `Api\appsettings.json` | `Auth:UseNegotiate` | `false` = Dev-Stub; `true` = Windows Negotiate/Kerberos |
| `Api\appsettings.json` | `Urls` | Kestrel-Bindung (Standard `http://localhost:5080`) |
| `Api\appsettings.json` / `Worker\appsettings.json` | `Gpd:Ad:ForceDryRun` | siehe nächster Abschnitt |
| beide | `Gpd:Ad:ServiceAccount` | geplantes Servicekonto / gMSA-Name |

Die Packaging-Vorlagen setzen **sichere Defaults fürs Lab**: `ForceDryRun=true`, `UseNegotiate=false`, SMTP/SQL als Platzhalter.

UI wird von der API aus `wwwroot` ausgeliefert — kein separater Node-Prozess nötig.

## ForceDryRun vs. Live-AD

| Einstellung | Wirkung |
|-------------|---------|
| `Gpd:Ad:ForceDryRun: true` | **Alle** AD/GPMC-Schreibvorgänge werden nur simuliert (DryRun), unabhängig von Domänenbeitritt |
| `Gpd:Ad:ForceDryRun: false` | Auto-Detect: ohne Domäne/GPMC weiterhin DryRun; mit Domäne + erreichbarem Verzeichnis + GPMC → **Live** |

Empfohlene Reihenfolge im AD-Lab:

1. Mit Packaging-Default (`ForceDryRun=true`) installieren und UI/API prüfen (`http://localhost:5080`, `GET /api/health`).
2. SQL-/SMTP-Platzhalter setzen; Auth bei Bedarf auf Negotiate umstellen (`Auth:UseNegotiate=true`).
3. Host domänenjoinen, GPMC bereitstellen, Servicekonto vorbereiten.
4. Erst dann in **beiden** `appsettings.json` (`Api` und `Worker`) `ForceDryRun` auf `false` setzen und Dienste neu starten:

```powershell
Restart-Service GroupPolicyDirectorApi, GroupPolicyDirectorWorker
```

Health/`/api/ad/environment` (bzw. UI-AD-Hinweis) zeigt `ForceDryRun` und den effektiven Modus.

## Dienste

| Dienstname | Anzeigename | Binary |
|------------|-------------|--------|
| `GroupPolicyDirectorApi` | Group Policy Director API | `...\Api\GroupPolicyDirector.Api.exe` |
| `GroupPolicyDirectorWorker` | Group Policy Director Worker | `...\Worker\GroupPolicyDirector.Worker.exe` |

## Hinweise

- Persistenz ist derzeit in-memory (SQL-Schema dokumentarisch) — Connection String ist vorbereitet für spätere Migrations.
- Intune-/Entra-Pfade sind deferred.
- Kein Live-AD nötig, um das Paket zu bauen oder lokal im DryRun zu testen.
