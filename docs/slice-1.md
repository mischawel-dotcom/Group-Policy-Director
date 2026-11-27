# Slice 1 — Foundation + GPO Checkout/Historie

Umsetzung gemäß Build-Plan §8.

## Enthalten

1. ASP.NET Core API + Worker + Core/Infrastructure
2. SQL-Schema-Stubs (`database/schema/001_initial.sql`)
3. Windows Auth Platzhalter (Dev-Stub / Negotiate-Schalter)
4. Product Admin Bootstrap + OU/GPO-Delegation an AD-Gruppen
5. Domain registrieren, Take Control, Checkout, Settings-Tree (read-only), Check-in, Historie
6. OOB-Erkennung + SMTP-Mail-Stub (kein Auto-Reset)
7. AGPM-Import Dry-Run Stub
8. React UI Skeleton

## Bewusst nicht enthalten

Intune, Entra SSO, Approve/Deploy-UI, Settings-Write, Container-Links, Tier-Gates, Auto-OOB-Reset.
