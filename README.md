# Group Policy Director

Moderner AGPM-Nachfolger mit Change-Control für Group Policy (on-prem first).  
**Slice 5:** AD Container-Links (OU/Site/Domain gpLink) unter Take Control + Checkout/Approve/Deploy, History/Diff-UI, SMTP-Polish für OOB (kein Auto-Reset). Branding: **Referenz-Tools**.

## Struktur

```
src/GroupPolicyDirector.Api/             ASP.NET Core REST-API
src/GroupPolicyDirector.Worker/          Hintergrunddienst (OOB-Scan)
src/GroupPolicyDirector.Core/            Domain & Contracts (inkl. Ad/, Agpm/)
src/GroupPolicyDirector.Infrastructure/  AD adapters + Services
ui/                                      React + TypeScript (Vite)
database/schema/                         SQL Server Schema-Stubs
fixtures/agpm-history-sample.zip         AGPM-style dry-run fixture
docs/slice-1.md … docs/slice-5.md        Slice-Notizen
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

## Workflow (Slice 5)

1. Domain registrieren → **Take Control** GPO und/oder **Container** (OU/Site/Domain)
2. **Delegation:** Product Admin vergibt OU/GPO/Container-Rollen an AD-Gruppen
3. **Editor:** Checkout → Settings Tree / Links-Editor → Check-in  
4. **Approver:** Diff prüfen → Approve (oder Reject)  
5. **Deployer:** Deploy (Temp→Production / gpLink write)  
6. **Historie:** Versionen vergleichen (Settings- oder Links-Diff)  
7. **SMTP:** Settings speichern / Test; OOB bleibt Mail-first ohne Auto-Reset  
8. **AGPM Dry-Run:** Fixture-ZIP (Slice 4) weiterhin verfügbar

SoD und AD-Admin-Gruppen-Trustee wie Referenz-Tools. Demo-Akteur: Header `X-Gpd-Actor`.

## API (Auszug)

| Methode | Pfad | Zweck |
|---------|------|--------|
| GET | `/api/health` | Health + AD mode (slice=5) |
| GET | `/api/ad/environment` | Domain-join / GPMC / DryRun |
| POST | `/api/gpos/take-control` | Take Control GPO |
| GET | `/api/gpos/{id}/diff` | Settings Diff zwischen Versionen |
| POST | `/api/containers/take-control` | Take Control Container |
| GET/PUT | `/api/containers/{id}/links` | gpLink working copy |
| POST | `/api/containers/{id}/checkout` / `check-in` / `approve` / `reject` / `deploy` | Container-Workflow |
| GET | `/api/containers/{id}/diff` | Links Diff |
| GET/PUT | `/api/settings/smtp` | SMTP runtime config |
| POST | `/api/settings/smtp/test` | Test-Mail (oder Log-only) |
| POST/GET | `/api/oob/scan`, `/api/oob` | OOB detection |
| GET/POST/DELETE | `/api/delegation/*` | Product + object roles |
| POST | `/api/agpm/import/dry-run` | AGPM dry-run |

## Auth / AD / SMTP

- **Auth Dev:** `Auth:UseNegotiate=false` → Stub + `X-Gpd-Actor`
- **AD:** `Gpd:Ad:ForceDryRun` erzwingt DryRun; sonst Auto-Detect
- **SMTP:** Runtime-Overlay über `Smtp:*`; Host leer = log-only; **kein Auto-Reset**

## Nicht in Slice 5

Geplantes Deploy, Auto-OOB-Reset, Intune, Entra SSO, Tier-/ESAE-Gates, SQL-Persistenz, vollständiger GPMC-COM CopyGPO.

## Lizenz / Marke

Produktname: **Group Policy Director** (GPD). Neue Marke — kein Klon bestehender Referenz-Tools.
