# Slice 3 — AD Take Control / Temp-GPO + OOB Mail

Fortsetzung von Slice 2. Branch: `cursor/gpd-slice3-ad-oob-1f00` (stack on PR #3).

## Enthalten

1. **AD boundary** (`IDirectoryEnvironment`, `IGpoDirectoryService`)
   - Domain-join / directory / GPMC detection
   - **Live** LDAP hooks when domain-joined (fingerprint, Take Control bind, Temp-GPO verify)
   - **DryRun** when WORKGROUP / ForceDryRun / no GPMC — planned steps, no AD writes
2. **Workflow wiring**
   - Take Control → directory TakeControl + baseline seed
   - Checkout → CreateTempGpo (Temp id) + existing OOB gate
   - Deploy / Reject → DeployTemp / DeleteTemp hooks
   - Settings Tree + Approve/Deploy/SoD unchanged in behaviour
3. **OOB mail-first**
   - Compare last controlled hash vs live fingerprint (LDAP or dry-run store)
   - Configurable SMTP (`Smtp:Host/Port/From/UseSsl/User/Password`)
   - Empty Host → log-only; **no auto-reset**
   - `POST /api/ad/simulate-drift` for offline demos
4. **UI**
   - AD environment panel, Temp id, OOB list, simulate-drift + scan

## Config

```json
"Gpd": { "Ad": { "ForceDryRun": false, "ServiceAccount": "GPD-Svc" } },
"Smtp": { "Host": "", "Port": 25, "From": "gpd-noreply@contoso.local", "UseSsl": false },
"Oob": { "AlertRecipients": ["gpd-admins@contoso.local"], "ScanIntervalSeconds": 300 }
```

## Bewusst nicht enthalten

Full GPMC COM CopyGPO/Import (hook + LDAP verify only), ACE write soak-tests, Auto-OOB-Reset, Tier-Gates, Intune/SSO, SQL persistence.
