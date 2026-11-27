# Group Policy Director

Moderner AGPM-Nachfolger mit Change-Control für Group Policy (on-prem first).  
**Slice 1:** Foundation — API, UI-Skeleton, SQL-Schema-Stubs, Windows-Auth-Platzhalter, Delegation, Take Control / Checkout / Historie, OOB-Mail-Stubs, AGPM-Import-Dry-Run.

## Struktur

```
src/GroupPolicyDirector.Api/             ASP.NET Core REST-API
src/GroupPolicyDirector.Worker/          Hintergrunddienst (OOB-Scan)
src/GroupPolicyDirector.Core/            Domain & Contracts
src/GroupPolicyDirector.Infrastructure/  In-Memory-Stubs (SQL folgt)
ui/                                      React + TypeScript (Vite)
database/schema/001_initial.sql          SQL Server Schema-Stubs
```

## Voraussetzungen

- .NET 9 SDK
- Node.js 20+ (für die UI)
- Windows (Zielplattform); lokale Dev ohne Domäne nutzt Auth-Stub

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

## Slice-1-API (Auszug)

| Methode | Pfad | Zweck |
|---------|------|--------|
| GET | `/api/health` | Health |
| GET | `/api/me` | Windows-/Dev-Identität + Rollen |
| POST | `/api/domains` | Domain registrieren |
| POST | `/api/gpos/take-control` | GPO unter Kontrolle |
| POST | `/api/gpos/{id}/checkout` | Checkout (OOB-Hinweis möglich) |
| GET | `/api/gpos/{id}/settings-tree` | Read-only Policy-Tree |
| POST | `/api/gpos/{id}/check-in` | Check-in mit Kommentar |
| GET | `/api/gpos/{id}/history` | Historie |
| POST/GET | `/api/delegation/*` | Product-/Objektrollen an AD-Gruppen |
| POST | `/api/oob/scan` | OOB-Erkennung + Mail-Stub |
| POST | `/api/agpm/import/dry-run` | AGPM-Archiv Dry-Run |

## Auth

- **Entwicklung:** `Auth:UseNegotiate=false` → Stub-Identität `CONTOSO\gpd-admin` (Product Admin).
- **Produktion (Platzhalter):** `Auth:UseNegotiate=true` aktiviert Windows Negotiate/Kerberos.

## Delegation

Product Admins vergeben im Tool Rechte auf **OU-** und **GPO-Ebene** (Editor / Approver / Deployer) an bestehende **AD-Admin-Rollengruppen**. Gruppenmitgliedschaft wird zur Login-Zeit ausgewertet (Slice 1: Stub-Match).

## OOB (Mail-first)

Erkennung von Drift außerhalb des Tools; SMTP-Stub loggt Mails. **Kein** Auto-Reset in Slice 1. Demo: GPO-Namen mit `OOB-DEMO` erzeugen künstlichen Drift.

## AGPM

Dry-Run scannt einen Archivpfad und liefert einen Report — **ohne** Historie zu schreiben (noch keine Sample-Archive).

## Nicht in Slice 1

Intune, Entra SSO, Approve/Deploy-UI, vollständiger Settings-Write, Container-Links, Tier-Gates, Auto-OOB-Reset.

## Lizenz / Marke

Produktname: **Group Policy Director** (GPD). Neue Marke — kein CMGPI-Klon.
