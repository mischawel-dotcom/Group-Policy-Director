# Group Policy Director

Moderner AGPM-Nachfolger mit Change-Control für Group Policy (on-prem first).  
**Slice 8:** Scheduled Deploy + Rollback-on-fail (GPO & Container), Deploy-Queue/Status-UI. Branding: **Referenz-Tools**. Kein Intune.

## Struktur

```
src/GroupPolicyDirector.Api/             ASP.NET Core REST-API (+ scheduled-deploy processor)
src/GroupPolicyDirector.Worker/          Hintergrunddienst (OOB-Scan + ProcessDue)
src/GroupPolicyDirector.Core/            Domain & Contracts (inkl. Ad/, Agpm/, Authz/)
src/GroupPolicyDirector.Infrastructure/  AD adapters + Services
ui/                                      React + TypeScript (Vite)
database/schema/                         SQL Server Schema-Stubs
fixtures/agpm-history-sample.zip         AGPM-style dry-run fixture
docs/slice-1.md … docs/slice-8.md        Slice-Notizen
```

## Voraussetzungen

- .NET 9 SDK
- Node.js 20+ (für die UI)
- Windows (Zielplattform); lokale Dev ohne Domäne nutzt **DryRun** + Auth-Stub + Demo-Akteure

## Schnellstart

```powershell
# API (http://localhost:5080)
dotnet run --project src/GroupPolicyDirector.Api

# UI (http://localhost:5173) — Proxies /api → API
cd ui
npm install
npm run dev
```

Optional Worker:

```powershell
dotnet run --project src/GroupPolicyDirector.Worker
```

## Windows-Paket (Lab)

Self-contained **win-x64** Publish von API (+ UI in `wwwroot`) und Worker, plus MSI/ZIP:

```bash
# Linux / Cloud (ZIP + MSI via wixl)
./packaging/build.sh
```

```powershell
# Windows (ZIP + MSI via WiX Toolset CLI)
.\packaging\build.ps1
```

Details (Voraussetzungen, Silent-Install, `ForceDryRun` vs. Live-AD): **[docs/install.md](docs/install.md)**.

## Workflow (Slice 8)

1. Editor → Check-in → Approver → Approve
2. Deployer: **Deploy now** oder **Schedule Deploy** (+ optional Rollback-on-fail)
3. Deploy Queue zeigt Pending / Succeeded / Failed / RolledBack / Cancelled
4. Fällige Jobs: Hosted Processor oder `POST /api/deploy-queue/process-due`
5. Simulate-fail-Flag für Rollback-Smoke (DryRun)

Demo-Akteur: Header `X-Gpd-Actor` (`gpd-admin`, `gpd-editor`, `gpd-approver`, `gpd-deployer`, `gpd-breakglass`, `gpd-auditor`).

## API (Auszug)

| Methode | Pfad | Zweck |
|---------|------|--------|
| GET | `/api/health` | Health + AD mode (slice=8) |
| POST | `/api/gpos/{id}/schedule-deploy` | GPO Deploy planen |
| POST | `/api/gpos/{id}/cancel-schedule` | GPO-Schedule stornieren |
| POST | `/api/containers/{id}/schedule-deploy` | Container Deploy planen |
| GET | `/api/deploy-queue` | Queue / Status |
| POST | `/api/deploy-queue/process-due` | Fällige Jobs manuell |
| … | Slice 7 endpoints | Naming, Restricted, Wizard, … |

## Auth / AD / SMTP

- **Auth Dev:** `Auth:UseNegotiate=false` → Stub + `X-Gpd-Actor`
- **AD:** `Gpd:Ad:ForceDryRun` erzwingt DryRun; sonst Auto-Detect
- **SMTP:** Runtime-Overlay über `Smtp:*`; Host leer = log-only; **kein Auto-Reset**
- **Schedule poll:** `ScheduledDeploy:PollIntervalSeconds` (Default 10)

## Nicht in Slice 8

Intune, Auto-OOB-Reset, Tier-/ESAE-Gates, Entra SSO, SQL-Persistenz.

## Lizenz / Marke

Produktname: **Group Policy Director** (GPD). Neue Marke — kein Klon bestehender Referenz-Tools.
