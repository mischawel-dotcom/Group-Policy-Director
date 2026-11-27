# Slice 2 — Approve / Deploy + Settings Tree

Fortsetzung von Slice 1 gemäß Build-Plan (Approve/Deploy als nächster vertikaler Schnitt).

## Enthalten

1. **Workflow:** Checkout → Settings edit → Check-in → **Approve / Reject** → **Deploy**
2. **SoD:** Editor, der die Pending-Version eingecheckt hat, darf weder Approve noch Deploy; Getrennte AD-Admin-Gruppen als Trustee (`GPD-Editors`, `GPD-Approvers`, `GPD-Deployers`)
3. **Settings Tree:** GPMC-vertraute Hierarchie (Computer/User → Policies/Preferences → Administrative Templates); editierbare Policy-States wo Stub-Daten vorhanden; echte ADMX/Security/Preferences als **Stub** markiert
4. **UI:** Demo-Akteur-Umschalter (`X-Gpd-Actor`) für SoD-Pfad ohne Domäne
5. **OOB-Mail-Stubs** unverändert; **keine** Tier-Gates

## Statusmodell

| Status | Bedeutung |
|--------|-----------|
| Under Control | Live / bereit für Checkout |
| Checked Out | Temp-Edit durch Editor |
| Waiting Approval | Nach Check-in |
| Approved | Freigegeben, wartet auf Deploy |
| (nach Deploy) | wieder Under Control |

## Demo-Pfad (ohne Domäne)

1. Product Admin: Domain registrieren, Take Control  
2. `CONTOSO\gpd-editor`: Checkout → Settings ändern → speichern → Check-in  
3. `CONTOSO\gpd-approver`: Approve  
4. `CONTOSO\gpd-deployer`: Deploy  

## Bewusst nicht enthalten

Intune, Entra SSO, Tier-/ESAE-Gates, Auto-OOB-Reset, echte AD/GPMC-Schreibzugriffe, Container-Links.
