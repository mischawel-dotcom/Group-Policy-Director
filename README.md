# Group Policy Director

Moderner AGPM-Nachfolger mit Change-Control für Group Policy (on-prem first).  
**Slice 7:** GPO Naming-Regex, Restricted Policies (Area-Deny), Welcome Wizard (skippable). Branding: **Referenz-Tools**. Kein Intune.

## Struktur

```
src/GroupPolicyDirector.Api/             ASP.NET Core REST-API
src/GroupPolicyDirector.Worker/          Hintergrunddienst (OOB-Scan)
src/GroupPolicyDirector.Core/            Domain & Contracts (inkl. Ad/, Agpm/, Authz/)
src/GroupPolicyDirector.Infrastructure/  AD adapters + Services
ui/                                      React + TypeScript (Vite)
database/schema/                         SQL Server Schema-Stubs
fixtures/agpm-history-sample.zip         AGPM-style dry-run fixture
docs/slice-1.md … docs/slice-7.md        Slice-Notizen
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

## Workflow (Slice 7)

1. Product Admin: optional **Welcome Wizard** (Complete / Skip)
2. Domain registrieren → Take Control und/oder **Create GPO** (Naming-Regex wenn aktiv)
3. **Restricted Policies:** Area-Pfad + Allowed Trustees → andere Editoren denied
4. Editor Checkout → Settings (restricted areas blockiert) → Check-in → Approve → Deploy
5. **Rename** unterliegt derselben Naming-Regel
6. Break Glass / Auditor / Audit / Diff / SMTP unverändert (kein Auto-Reset, kein Intune)

Demo-Akteur: Header `X-Gpd-Actor` (`gpd-admin`, `gpd-editor`, `gpd-approver`, `gpd-deployer`, `gpd-breakglass`, `gpd-auditor`).

## API (Auszug)

| Methode | Pfad | Zweck |
|---------|------|--------|
| GET | `/api/health` | Health + AD mode (slice=7) |
| GET/PUT | `/api/settings/general` | Naming regex + wizard status |
| POST | `/api/gpos` | Create GPO (naming enforced) |
| POST | `/api/gpos/{id}/rename` | Rename (naming enforced) |
| GET/POST/DELETE | `/api/restricted-policies` | Area-level Restricted Policies |
| GET/POST | `/api/wizard`, `/complete`, `/skip` | Welcome Wizard |
| … | Slice 6 endpoints | Break Glass, Audit, Delegation, Containers |

## Auth / AD / SMTP

- **Auth Dev:** `Auth:UseNegotiate=false` → Stub + `X-Gpd-Actor`
- **AD:** `Gpd:Ad:ForceDryRun` erzwingt DryRun; sonst Auto-Detect
- **SMTP:** Runtime-Overlay über `Smtp:*`; Host leer = log-only; **kein Auto-Reset**

## Nicht in Slice 7

Intune, Auto-OOB-Reset, Tier-/ESAE-Gates, Entra SSO, geplantes Deploy, SQL-Persistenz, Setting-Level Restricted Policies.

## Lizenz / Marke

Produktname: **Group Policy Director** (GPD). Neue Marke — kein Klon bestehender Referenz-Tools.
