# Slice 4 — AGPM Dry-Run Parser + Delegation Admin UI

Fortsetzung von Slice 3 (AD Take Control / OOB bleibt erhalten). Branding: **Referenz-Tools** (kein CMGPI/SDM).

## Enthalten

1. **AGPM archive dry-run**
   - Stub-Parser für Ordner/.zip mit GUID-Ordnern, `Backup.xml`, optional `manifest.json`
   - Mapping gegen kontrollierte GPOs; Report mit Entries + Conflicts
   - API: `POST /api/agpm/import/dry-run`, `POST /api/agpm/import/dry-run/upload`, `GET /api/agpm/fixture`
   - Fixture: `fixtures/agpm-history-sample.zip` (kein echtes Kundenarchiv nötig)
   - **Kein** History-Write
2. **Delegation Admin UI**
   - OU-/GPO-Objektrollen an AD-Admin-Rollengruppen (Grant/Revoke)
   - Produktrollen Grant/Revoke
   - Suggested trustees + scope hints
3. **Slice 3 unverändert nutzbar**
   - AD environment, Take Control / Temp-GPO, OOB SMTP, Approve/Deploy/Settings Tree

## Bewusst nicht enthalten

AGPM Write-Import, SQL-Persistenz, Tier-Gates, Intune/SSO, vollständiger AGPM-Binärformat-Parser.
