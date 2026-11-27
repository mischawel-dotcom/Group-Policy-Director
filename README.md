# Group Policy Director

Moderner AGPM-Nachfolger mit Change-Control für Group Policy (on-prem first).  
**Slice 2:** Approve / Deploy mit SoD, editierbarer GPMC-Settings-Tree, End-to-End-Workflow in der UI (AD-Fakes ohne Domäne).

## Struktur

```
src/GroupPolicyDirector.Api/             ASP.NET Core REST-API
src/GroupPolicyDirector.Worker/          Hintergrunddienst (OOB-Scan)
src/GroupPolicyDirector.Core/            Domain & Contracts
src/GroupPolicyDirector.Infrastructure/  In-Memory-Stubs (SQL folgt)
ui/                                      React + TypeScript (Vite)
database/schema/                         SQL Server Schema-Stubs
docs/slice-1.md, docs/slice-2.md         Slice-Notizen
```

## Voraussetzungen

- .NET 9 SDK
- Node.js 20+ (für die UI)
- Windows (Zielplattform); lokale Dev ohne Domäne nutzt Auth-Stub + Demo-Akteure

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

## Workflow (Slice 2)

1. Domain registrieren → Take Control  
2. **Editor** (`CONTOSO\gpd-editor`): Checkout → Settings Tree editieren → Check-in  
3. **Approver** (`CONTOSO\gpd-approver`): Approve (oder Reject)  
4. **Deployer** (`CONTOSO\gpd-deployer`): Deploy  

SoD: der Editor der Pending-Version darf weder Approve noch Deploy. Trustee sind AD-Admin-Rollengruppen (`CONTOSO\GPD-Editors` / `Approvers` / `Deployers`); Mitgliedschaft zur Login-Zeit (Stub-Tabelle ohne Domäne).

Demo-Akteur in der UI umschalten — API-Header `X-Gpd-Actor`.

## API (Auszug)

| Methode | Pfad | Zweck |
|---------|------|--------|
| GET | `/api/me` | Identität, Rollen, Demo-Akteure |
| POST | `/api/domains` | Domain registrieren |
| POST | `/api/gpos/take-control` | GPO unter Kontrolle |
| POST | `/api/gpos/{id}/checkout` | Checkout |
| GET/PUT | `/api/gpos/{id}/settings-tree` | Policy-Tree lesen / editieren |
| POST | `/api/gpos/{id}/check-in` | Check-in → Waiting Approval |
| POST | `/api/gpos/{id}/approve` | Freigabe |
| POST | `/api/gpos/{id}/reject` | Ablehnung |
| POST | `/api/gpos/{id}/deploy` | Deploy (Produktion-Stub) |
| GET | `/api/gpos/{id}/history` | Historie |
| POST/GET | `/api/delegation/*` | Product-/Objektrollen an AD-Gruppen |
| POST | `/api/oob/scan` | OOB-Erkennung + Mail-Stub |
| POST | `/api/agpm/import/dry-run` | AGPM-Archiv Dry-Run |

## Auth

- **Entwicklung:** `Auth:UseNegotiate=false` → Stub + optional `X-Gpd-Actor`.
- **Produktion (Platzhalter):** `Auth:UseNegotiate=true` aktiviert Windows Negotiate/Kerberos.

## OOB (Mail-first)

Unverändert: Drift-Erkennung + SMTP-Stub. **Kein** Auto-Reset. **Keine** Tier-Gates.

## Nicht in Slice 2

Intune, Entra SSO, Tier-/ESAE-Gates, Auto-OOB-Reset, echte AD/GPMC-Schreibpfade, Container-Links.

## Lizenz / Marke

Produktname: **Group Policy Director** (GPD). Neue Marke — kein CMGPI-Klon.
