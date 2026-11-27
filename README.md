# Group Policy Director

Moderner AGPM-Nachfolger mit Change-Control für Group Policy (on-prem first).  
**Slice 4:** AGPM-Archiv Dry-Run (Parser + Report) und Admin-UI für OU/GPO-Delegation an AD-Rollengruppen — auf Slice 3 (AD Temp-GPO / OOB SMTP).

## Struktur

```
src/GroupPolicyDirector.Api/             ASP.NET Core REST-API
src/GroupPolicyDirector.Worker/          Hintergrunddienst (OOB-Scan)
src/GroupPolicyDirector.Core/            Domain & Contracts (inkl. Ad/, Agpm/)
src/GroupPolicyDirector.Infrastructure/  AD adapters + Services
ui/                                      React + TypeScript (Vite)
database/schema/                         SQL Server Schema-Stubs
fixtures/agpm-history-sample.zip         AGPM-style dry-run fixture
docs/slice-1.md … docs/slice-4.md        Slice-Notizen
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

## Workflow (Slice 4)

1. Domain registrieren → **Take Control** (ACL-Plan live/dry-run + Baseline)
2. **Delegation:** Product Admin vergibt OU/GPO-Rollen an AD-Gruppen (`GPD-Editors` / Approvers / Deployers)
3. **Editor** (`CONTOSO\gpd-editor`): Checkout (Temp-GPO) → Settings Tree → Check-in  
4. **Approver** (`CONTOSO\gpd-approver`): Approve (oder Reject)  
5. **Deployer** (`CONTOSO\gpd-deployer`): Deploy (Temp → Production hook)  
6. **AGPM Dry-Run:** Fixture-ZIP hochladen oder Pfad angeben → Mapping-Report (kein Write)

OOB: `Simulate live drift` (nur DryRun) → `OOB-Scan + SMTP`. Kein Auto-Reset.

SoD und AD-Admin-Gruppen-Trustee wie Referenz-Tools. Demo-Akteur: Header `X-Gpd-Actor`.

## API (Auszug)

| Methode | Pfad | Zweck |
|---------|------|--------|
| GET | `/api/health` | Health + AD mode (slice=4) |
| GET | `/api/ad/environment` | Domain-join / GPMC / DryRun |
| POST | `/api/ad/simulate-drift` | DryRun OOB demo |
| POST | `/api/gpos/take-control` | Take Control (+ AD hook) |
| POST | `/api/gpos/{id}/checkout` | Checkout Temp-GPO |
| GET/PUT | `/api/gpos/{id}/settings-tree` | Policy-Tree |
| POST | `/api/gpos/{id}/check-in` / `approve` / `reject` / `deploy` | Workflow |
| POST/GET | `/api/oob/scan`, `/api/oob` | OOB detection + list |
| GET/POST/DELETE | `/api/delegation/*` | Product + object roles, trustees, hints |
| POST | `/api/agpm/import/dry-run` | Path-based dry-run |
| POST | `/api/agpm/import/dry-run/upload` | Zip upload dry-run |
| GET | `/api/agpm/fixture` | Bundled sample zip path |

## Auth / AD / SMTP

- **Auth Dev:** `Auth:UseNegotiate=false` → Stub + `X-Gpd-Actor`
- **AD:** `Gpd:Ad:ForceDryRun` erzwingt DryRun; sonst Auto-Detect
- **SMTP:** `Smtp:Host` leer = log-only; gesetzt = echter SMTP-Send

## Nicht in Slice 4

AGPM History-Write, Intune, Entra SSO, Tier-/ESAE-Gates, Auto-OOB-Reset, vollständiger GPMC-COM CopyGPO, Container-Links, SQL-Persistenz.

## Lizenz / Marke

Produktname: **Group Policy Director** (GPD). Neue Marke — kein Klon bestehender Referenz-Tools.
