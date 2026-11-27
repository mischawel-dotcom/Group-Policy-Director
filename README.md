# Group Policy Director

Moderner AGPM-Nachfolger mit Change-Control für Group Policy (on-prem first).  
**Slice 6:** Break Glass (zeitgebunden, auditierter SoD-Bypass), Auditor (read-only), Audit-Log Filter + CSV. Branding: **Referenz-Tools**. Kein Intune.

## Struktur

```
src/GroupPolicyDirector.Api/             ASP.NET Core REST-API
src/GroupPolicyDirector.Worker/          Hintergrunddienst (OOB-Scan)
src/GroupPolicyDirector.Core/            Domain & Contracts (inkl. Ad/, Agpm/, Authz/)
src/GroupPolicyDirector.Infrastructure/  AD adapters + Services
ui/                                      React + TypeScript (Vite)
database/schema/                         SQL Server Schema-Stubs
fixtures/agpm-history-sample.zip         AGPM-style dry-run fixture
docs/slice-1.md … docs/slice-6.md        Slice-Notizen
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

## Workflow (Slice 6)

1. Domain registrieren → **Take Control** GPO und/oder **Container**
2. **Delegation:** Product Admin vergibt OU/GPO/Container-Rollen an AD-Gruppen
3. **Editor → Approver → Deployer** wie Slice 5 (SoD)
4. **Break Glass:** Product Admin aktiviert Session für Break-Glass-Trustee (Reason, Dauer) → Notfall-Bypass → Revoke/Expiry
5. **Auditor:** Demo-Akteur `CONTOSO\gpd-auditor` — read-only Objekte/Diffs/Audit
6. **Audit Log:** Filter Object/Actor/Action/Zeit → Export CSV
7. History/Diff, SMTP/OOB, AGPM Dry-Run unverändert (kein Auto-Reset, kein Intune)

Demo-Akteur: Header `X-Gpd-Actor` (`gpd-admin`, `gpd-editor`, `gpd-approver`, `gpd-deployer`, `gpd-breakglass`, `gpd-auditor`).

## API (Auszug)

| Methode | Pfad | Zweck |
|---------|------|--------|
| GET | `/api/health` | Health + AD mode (slice=6) |
| GET/POST | `/api/break-glass`, `/activate`, `/{id}/revoke` | Break Glass sessions |
| GET | `/api/audit` | Audit filter (object/actor/action/from/to) |
| GET | `/api/audit/export` | Audit CSV export |
| GET | `/api/me` | Identity + roles + breakGlassActive / isAuditor |
| … | Slice 5 endpoints | Containers, Diff, SMTP, Delegation, AGPM |

## Auth / AD / SMTP

- **Auth Dev:** `Auth:UseNegotiate=false` → Stub + `X-Gpd-Actor`
- **AD:** `Gpd:Ad:ForceDryRun` erzwingt DryRun; sonst Auto-Detect
- **SMTP:** Runtime-Overlay über `Smtp:*`; Host leer = log-only; **kein Auto-Reset**

## Nicht in Slice 6

Intune, Auto-OOB-Reset, Tier-/ESAE-Gates, Entra SSO, geplantes Deploy, SQL-Persistenz, vollständiger GPMC-COM CopyGPO.

## Lizenz / Marke

Produktname: **Group Policy Director** (GPD). Neue Marke — kein Klon bestehender Referenz-Tools.
