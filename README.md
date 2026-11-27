# Group Policy Director

Moderner AGPM-Nachfolger mit Change-Control für Group Policy (on-prem first).  
**Slice 3:** Take Control / Temp-GPO AD-Pfad (LDAP live oder DryRun), OOB baseline-vs-live + konfigurierbares SMTP, verdrahtet in Approve/Deploy/Settings Tree.

## Struktur

```
src/GroupPolicyDirector.Api/             ASP.NET Core REST-API
src/GroupPolicyDirector.Worker/          Hintergrunddienst (OOB-Scan)
src/GroupPolicyDirector.Core/            Domain & Contracts (inkl. Ad/)
src/GroupPolicyDirector.Infrastructure/  AD adapters + Services
ui/                                      React + TypeScript (Vite)
database/schema/                         SQL Server Schema-Stubs
docs/slice-1.md … docs/slice-3.md        Slice-Notizen
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

## Workflow (Slice 3)

1. Domain registrieren → **Take Control** (ACL-Plan live/dry-run + Baseline)
2. **Editor** (`CONTOSO\gpd-editor`): Checkout (Temp-GPO) → Settings Tree → Check-in  
3. **Approver** (`CONTOSO\gpd-approver`): Approve (oder Reject)  
4. **Deployer** (`CONTOSO\gpd-deployer`): Deploy (Temp → Production hook)  

OOB: `Simulate live drift` (nur DryRun) → `OOB-Scan + SMTP`. Kein Auto-Reset.

SoD und AD-Admin-Gruppen-Trustee wie Slice 2. Demo-Akteur: Header `X-Gpd-Actor`.

## API (Auszug)

| Methode | Pfad | Zweck |
|---------|------|--------|
| GET | `/api/health` | Health + AD mode |
| GET | `/api/ad/environment` | Domain-join / GPMC / DryRun |
| POST | `/api/ad/simulate-drift` | DryRun OOB demo |
| POST | `/api/gpos/take-control` | Take Control (+ AD hook) |
| POST | `/api/gpos/{id}/checkout` | Checkout Temp-GPO |
| GET/PUT | `/api/gpos/{id}/settings-tree` | Policy-Tree |
| POST | `/api/gpos/{id}/check-in` / `approve` / `reject` / `deploy` | Workflow |
| POST/GET | `/api/oob/scan`, `/api/oob` | OOB detection + list |

## Auth / AD / SMTP

- **Auth Dev:** `Auth:UseNegotiate=false` → Stub + `X-Gpd-Actor`
- **AD:** `Gpd:Ad:ForceDryRun` erzwingt DryRun; sonst Auto-Detect
- **SMTP:** `Smtp:Host` leer = log-only; gesetzt = echter SMTP-Send

## Nicht in Slice 3

Intune, Entra SSO, Tier-/ESAE-Gates, Auto-OOB-Reset, vollständiger GPMC-COM CopyGPO, Container-Links, SQL-Persistenz.

## Lizenz / Marke

Produktname: **Group Policy Director** (GPD). Neue Marke — kein Klon bestehender Referenz-Tools.
