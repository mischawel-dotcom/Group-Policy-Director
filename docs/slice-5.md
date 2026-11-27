# Slice 5 — AD Container-Links + History/Diff + SMTP Polish

Fortsetzung von Slice 4. Branding: **Referenz-Tools** (kein CMGPI/SDM).

## Enthalten

1. **AD Container-Links (OU / Site / Domain)**
   - Take Control für Container (`gpLink` / `gpOptions` ACL-Pfad)
   - Checkout → Links-Editor (Order, Enforce, Disable, Add/Remove, Block Inheritance)
   - Check-in → Approve → Reject → Deploy (Live LDAP oder DryRun)
   - API: `/api/containers/*`
2. **History / Diff UI**
   - GPO: Version wählen, Settings-Diff (`GET /api/gpos/{id}/diff?from=&to=`)
   - Container: Links-Diff (`GET /api/containers/{id}/diff?from=&to=`)
3. **SMTP Config Polish (OOB)**
   - Runtime-Overlay: `GET/PUT /api/settings/smtp`, `POST /api/settings/smtp/test`
   - Host leer = Log-only; Alert-Recipients editierbar
   - **Kein Auto-Reset** (`autoResetEnabled` immer false)

## Bewusst nicht enthalten

Geplantes Deploy, Auto-OOB-Reset, Tier-Gates, Intune/SSO, SQL-Persistenz, volle GPMC-COM CopyGPO.
