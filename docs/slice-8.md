# Slice 8 — Scheduled Deploy + Rollback-on-fail

Fortsetzung von Slice 7. Branding: **Referenz-Tools** (kein CMGPI/SDM). **Kein Intune.**

## Enthalten

1. **Scheduled Deploy (GPO & Container)**
   - `POST /api/gpos/{id}/schedule-deploy` · `POST /api/containers/{id}/schedule-deploy`
   - Body: `scheduledFor`, `rollbackOnFailure` (default true), optional `simulateFailure` (smoke)
   - Object-Status → `Scheduled` (6); Cancel → zurück `Approved`
   - `POST …/cancel-schedule` oder `POST /api/deploy-queue/{id}/cancel`
2. **Rollback-on-failure**
   - Bei Deploy-Fehler (inkl. Simulate): letzte kontrollierte Version/Links wiederherstellen
   - Audit: `Gpo.DeployRollback` / `Container.DeployRollback`
   - Queue-Status: `RolledBack` vs. `Failed` (ohne Rollback)
3. **Deploy Queue / Status UI**
   - `GET /api/deploy-queue`, `POST /api/deploy-queue/process-due`
   - API Hosted Service pollt fällige Jobs (shared in-memory store)
4. Sofort-Deploy unverändert: `POST …/deploy` mit optionalem Body `{ rollbackOnFailure, simulateFailure }`

## Bewusst nicht enthalten

Intune, Auto-OOB-Reset, Tier-Gates, Entra SSO, SQL-Persistenz (Queue teilt Store erst mit SQL zwischen API/Worker).
