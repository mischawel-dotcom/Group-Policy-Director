# Slice 6 — Break Glass + Auditor + Audit Log UI

Fortsetzung von Slice 5. Branding: **Referenz-Tools** (kein CMGPI/SDM). **Kein Intune.**

## Enthalten

1. **Break Glass (Produktrolle + zeitgebundene Session)**
   - Eligibility: `ProductRole.BreakGlass` an AD-Gruppe/User
   - Activation nur durch Product Admin (4-Augen), Reason Pflicht, Default 1h / Max 8h
   - Aktive Session: Bypass Objektrollen + SoD für Checkout / Approve / Deploy
   - Audit: `BreakGlass.Activate` / `Revoke` / `Use`
   - API: `/api/break-glass`
2. **Auditor (Produktrolle)**
   - Read-only über Objekte, Diffs, Audit
   - Mutationen für Auditor-only werden abgewiesen
3. **Audit Log UI**
   - Filter: Object / Actor / Action / Zeit (from/to)
   - CSV-Export: `GET /api/audit/export`

## Bewusst nicht enthalten

Intune, Auto-OOB-Reset, Tier-Gates, Entra SSO, SQL-Persistenz, geplantes Deploy.
