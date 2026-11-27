# Slice 7 — Naming / Restricted Policies / Welcome Wizard

Fortsetzung von Slice 6. Branding: **Referenz-Tools** (kein CMGPI/SDM). **Kein Intune.**

## Enthalten

1. **GPO Naming Regex**
   - General Settings: `GET/PUT /api/settings/general`
   - Enforcement bei **Create** und **Rename** (nicht bei Take Control bestehender Produktionsnamen)
   - Leeres Regex = Enforcement aus
2. **Create / Rename GPO**
   - `POST /api/gpos` — GpoCreator oder Product Admin; optional Copy + Checkout
   - `POST /api/gpos/{id}/rename` — Editor / Product Admin / Break Glass
3. **Restricted Policies (Area-level)**
   - Product Admin: `GET/POST/DELETE /api/restricted-policies`
   - Allowed Trustees ⇒ Implicit Deny für andere Editoren
   - `UpdateSettings` und `Check-in` blockieren bei Verstoß
   - Product Admin / aktive Break-Glass-Session bypass
4. **Welcome Wizard (skippable)**
   - Nur Product Admin; Status Pending / Completed / Skipped
   - Schritte: Domain & GPOs, AD Containers (optional), Delegation, Settings (Intune deferred)
   - `GET /api/wizard`, `POST /api/wizard/complete`, `POST /api/wizard/skip`

## Bewusst nicht enthalten

Intune, Auto-OOB-Reset, Tier-Gates, Entra SSO, SQL-Persistenz, Setting-Level Restricted Policies, geplantes Deploy.
