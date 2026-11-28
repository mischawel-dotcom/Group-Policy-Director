import { useGpd } from '../gpd/GpdContext'
import {
  adModeLabel,
  objectRoleLabel,
  productRoleLabel,
  scopeKindLabel,
} from '../labels'

export function AdminView() {
  const g = useGpd()

  return (
    <>
      <section className="panel view-intro">
        <h2>Admin / Delegation</h2>
        <p className="muted">
          AD-Umgebung, Objekt- und Produktrollen, Break Glass, Restricted Policies, SMTP und AGPM Dry-Run.
        </p>
      </section>

      <section className="panel">
        <h2>AD / GPMC Environment</h2>
        {g.adEnv ? (
          <ul className="plain env-list">
            <li>
              Mode: <strong>{adModeLabel(g.adEnv.effectiveMode)}</strong>
              {g.adEnv.forceDryRunConfigured ? ' (ForceDryRun)' : ''}
            </li>
            <li>Domain-joined: {g.adEnv.isDomainJoined ? 'yes' : 'no'}</li>
            <li>Directory reachable: {g.adEnv.directoryReachable ? 'yes' : 'no'}</li>
            <li>GPMC detected: {g.adEnv.gpmcAvailable ? 'yes' : 'no'}</li>
            {g.adEnv.computerDnsDomain ? <li>DNS domain: {g.adEnv.computerDnsDomain}</li> : null}
            <li className="muted">{g.adEnv.modeReason}</li>
          </ul>
        ) : (
          <p className="muted">Loading environment…</p>
        )}
      </section>

      <section className="panel delegation-panel">
        <h2>Delegation (OU / GPO / Container → AD-Gruppen)</h2>
        <p className="muted">
          Product Admin vergibt Objektrollen an bestehende AD-Admin-Rollengruppen (Referenz-Tools-Modell). SoD: Editor ≠ Approver/Deployer.
        </p>

        <h3>Objektrollen</h3>
        <div className="row">
          <label>
            Scope
            <select
              value={g.delScopeKind}
              onChange={(e) => {
                const kind = Number(e.target.value)
                g.setDelScopeKind(kind)
                const hint = g.scopeHints.find((h) => h.scopeKind === kind)
                if (hint) g.setDelScopeKey(hint.scopeKey)
              }}
            >
              <option value={1}>OU</option>
              <option value={2}>GPO</option>
              <option value={3}>Container (Site/Domain/OU)</option>
            </select>
          </label>
          <label>
            Scope-Key
            <input
              list="scope-hints"
              value={g.delScopeKey}
              onChange={(e) => g.setDelScopeKey(e.target.value)}
            />
            <datalist id="scope-hints">
              {g.scopeHints
                .filter((h) => h.scopeKind === g.delScopeKind)
                .map((h) => (
                  <option key={`${h.scopeKind}-${h.scopeKey}`} value={h.scopeKey}>
                    {h.label ?? h.scopeKey}
                  </option>
                ))}
            </datalist>
          </label>
          <label>
            Rolle
            <select value={g.delRole} onChange={(e) => g.setDelRole(Number(e.target.value))}>
              <option value={1}>Editor</option>
              <option value={2}>Approver</option>
              <option value={3}>Deployer</option>
            </select>
          </label>
          <label>
            Trustee (AD-Gruppe)
            <input
              list="trustee-hints"
              value={g.delTrustee}
              onChange={(e) => g.setDelTrustee(e.target.value)}
            />
            <datalist id="trustee-hints">
              {g.trustees.map((t) => (
                <option key={t.principal} value={t.principal}>
                  {t.description ?? t.kind}
                </option>
              ))}
            </datalist>
          </label>
          <label className="checkbox-label">
            Vererbung
            <input
              type="checkbox"
              checked={g.delInherit}
              onChange={(e) => g.setDelInherit(e.target.checked)}
            />
          </label>
          <button type="button" onClick={() => void g.grantObjectRole()}>
            Gewähren
          </button>
        </div>
        <ul className="delegation-list">
          {g.objectRoles.map((grant) => (
            <li key={grant.id}>
              <span>
                <strong>{scopeKindLabel(grant.scopeKind)}</strong> {grant.scopeKey}
                {' · '}
                {objectRoleLabel(grant.role)} → {grant.trusteePrincipal}
                {grant.inheritToChildren ? ' (inherit)' : ''}
              </span>
              <button type="button" className="secondary" onClick={() => void g.revokeObjectRole(grant.id)}>
                Revoke
              </button>
            </li>
          ))}
        </ul>

        <h3>Produktrollen</h3>
        <div className="row">
          <label>
            Rolle
            <select value={g.prodRole} onChange={(e) => g.setProdRole(Number(e.target.value))}>
              <option value={1}>ProductAdministrator</option>
              <option value={2}>GpoCreator</option>
              <option value={3}>BreakGlass</option>
              <option value={4}>Auditor</option>
            </select>
          </label>
          <label>
            Trustee
            <input
              list="trustee-hints"
              value={g.prodTrustee}
              onChange={(e) => g.setProdTrustee(e.target.value)}
            />
          </label>
          <button type="button" onClick={() => void g.grantProductRole()}>
            Gewähren
          </button>
        </div>
        <ul className="delegation-list">
          {g.productRoles.map((p) => (
            <li key={p.id}>
              <span>
                {productRoleLabel(p.role)} → {p.trusteePrincipal}
                <span className="muted"> · by {p.grantedBy}</span>
              </span>
              <button type="button" className="secondary" onClick={() => void g.revokeProductRole(p.id)}>
                Revoke
              </button>
            </li>
          ))}
        </ul>
      </section>

      <section className="panel breakglass-panel">
        <h2>Break Glass (Notfall)</h2>
        <p className="muted">
          Product Admin aktiviert eine zeitgebundene Session für einen Break-Glass-Trustee. Aktive Session bypassed Objekt-SoD (Checkout/Approve/Deploy) und wird auditiert. Kein dauerhafter Admin.
        </p>
        <div className="row">
          <label>
            Trustee
            <input
              list="trustee-hints"
              value={g.bgTrustee}
              onChange={(e) => g.setBgTrustee(e.target.value)}
            />
          </label>
          <label>
            Dauer (Min.)
            <input
              type="number"
              min={1}
              max={480}
              value={g.bgMinutes}
              onChange={(e) => g.setBgMinutes(Number(e.target.value) || 60)}
            />
          </label>
          <label className="grow">
            Reason
            <input value={g.bgReason} onChange={(e) => g.setBgReason(e.target.value)} />
          </label>
          <button type="button" onClick={() => void g.activateBreakGlass()}>
            Activate
          </button>
        </div>
        <ul className="delegation-list">
          {g.bgSessions.map((s) => {
            const active = !s.revokedAt && new Date(s.expiresAt).getTime() > Date.now()
            return (
              <li key={s.id}>
                <span>
                  <strong>{active ? 'ACTIVE' : s.revokedAt ? 'Revoked' : 'Expired'}</strong>
                  {' · '}
                  {s.trusteePrincipal}
                  <span className="muted">
                    {' · '}until {new Date(s.expiresAt).toLocaleString()}
                    {' · '}by {s.activatedBy}
                    {' · '}{s.reason}
                  </span>
                </span>
                {active ? (
                  <button type="button" className="secondary" onClick={() => void g.revokeBreakGlass(s.id)}>
                    Revoke
                  </button>
                ) : null}
              </li>
            )
          })}
        </ul>
      </section>

      <section className="panel">
        <h2>Restricted Policies (Area-level)</h2>
        <p className="muted">
          Product Admin setzt erlaubte Trustee für eine Policy-Area. Andere Editoren: Implicit Deny — Update und Check-in blockiert. Kein Setting-Level.
        </p>
        <div className="row">
          <label>
            Area Path
            <input
              list="area-hints"
              value={g.rpAreaPath}
              onChange={(e) => g.setRpAreaPath(e.target.value)}
            />
            <datalist id="area-hints">
              {g.areaHints.map((a) => (
                <option key={a} value={a} />
              ))}
            </datalist>
          </label>
          <label>
            Label
            <input value={g.rpLabel} onChange={(e) => g.setRpLabel(e.target.value)} />
          </label>
          <label>
            Allowed Trustees
            <input
              list="trustee-hints"
              value={g.rpTrustees}
              onChange={(e) => g.setRpTrustees(e.target.value)}
            />
          </label>
          <button type="button" onClick={() => void g.upsertRestricted()}>
            Speichern
          </button>
        </div>
        <ul className="delegation-list">
          {g.restricted.length === 0 ? (
            <li className="muted">Keine Restricted Areas konfiguriert.</li>
          ) : (
            g.restricted.map((r) => (
              <li key={r.id}>
                <span>
                  <strong>{r.label ?? r.areaPath}</strong>
                  <span className="muted"> · {r.areaPath}</span>
                  <br />
                  Allowed: {r.allowedTrustees.length ? r.allowedTrustees.join(', ') : '(none — deny all editors)'}
                </span>
                <button type="button" className="secondary" onClick={() => void g.deleteRestricted(r.id)}>
                  Remove
                </button>
              </li>
            ))
          )}
        </ul>
      </section>

      <section className="panel">
        <h2>SMTP (OOB Mail-first)</h2>
        <p className="muted">
          Runtime-Overlay für OOB-Alerts. Host leer = Log-only. Auto-Reset bleibt deaktiviert.
          {g.smtp ? ` Aktuell: ${g.smtp.mode}${g.smtp.passwordConfigured ? ' · Passwort gesetzt' : ''}.` : ''}
        </p>
        <div className="row">
          <label>
            Host
            <input value={g.smtpHost} onChange={(e) => g.setSmtpHost(e.target.value)} placeholder="leer = log-only" />
          </label>
          <label>
            Port
            <input
              type="number"
              value={g.smtpPort}
              onChange={(e) => g.setSmtpPort(Number(e.target.value) || 25)}
            />
          </label>
          <label>
            From
            <input value={g.smtpFrom} onChange={(e) => g.setSmtpFrom(e.target.value)} />
          </label>
          <label className="checkbox-label">
            SSL/TLS
            <input
              type="checkbox"
              checked={g.smtpUseSsl}
              onChange={(e) => g.setSmtpUseSsl(e.target.checked)}
            />
          </label>
        </div>
        <div className="row">
          <label>
            User
            <input value={g.smtpUser} onChange={(e) => g.setSmtpUser(e.target.value)} />
          </label>
          <label>
            Password {g.smtp?.passwordConfigured ? '(gesetzt — leer lassen behält)' : ''}
            <input
              type="password"
              value={g.smtpPassword}
              onChange={(e) => g.setSmtpPassword(e.target.value)}
              autoComplete="new-password"
            />
          </label>
          <label>
            Alert recipients
            <input value={g.smtpRecipients} onChange={(e) => g.setSmtpRecipients(e.target.value)} />
          </label>
        </div>
        <div className="actions">
          <button type="button" onClick={() => void g.saveSmtp()}>
            SMTP speichern
          </button>
          <input
            placeholder="Test-Empfänger (optional)"
            value={g.smtpTestTo}
            onChange={(e) => g.setSmtpTestTo(e.target.value)}
          />
          <button type="button" className="secondary" onClick={() => void g.testSmtp()}>
            Test senden
          </button>
        </div>
        <p className="muted">Auto-reset enabled: never (MVP mail-first).</p>
      </section>

      <section className="panel agpm-panel">
        <h2>AGPM Import Dry-Run</h2>
        <p className="muted">
          Archive-Parser (GUID-Ordner, Backup.xml, manifest.json, .zip). Kein History-Write — nur Report.
        </p>
        <div className="row">
          <label>
            Archivpfad (Ordner oder .zip)
            <input value={g.agpmPath} onChange={(e) => g.setAgpmPath(e.target.value)} />
          </label>
          <button type="button" onClick={() => void g.agpmDryRunPath()}>
            Dry-Run Pfad
          </button>
        </div>
        <div className="row">
          <label>
            Fixture / Archive Upload (.zip)
            <input
              type="file"
              accept=".zip,application/zip"
              onChange={(e) => g.setAgpmFile(e.target.files?.[0] ?? null)}
            />
          </label>
          <button type="button" onClick={() => void g.agpmDryRunUpload()}>
            Dry-Run Upload
          </button>
        </div>
        {g.agpmReport ? (
          <div className="agpm-report">
            <p>
              <strong>{g.agpmReport.parser}</strong>
              {' · '}
              scanned={g.agpmReport.archivesScanned}
              {' · '}
              versions={g.agpmReport.versionsFound}
              {' · '}
              mappable={g.agpmReport.mappableGpos}
              {' · '}
              unmapped={g.agpmReport.unmappedGpos}
            </p>
            <p className="muted">{g.agpmReport.notes}</p>
            {g.agpmReport.entries.length ? (
              <table className="report-table">
                <thead>
                  <tr>
                    <th>GPO</th>
                    <th>Versions</th>
                    <th>Mapping</th>
                    <th>Controlled Id</th>
                  </tr>
                </thead>
                <tbody>
                  {g.agpmReport.entries.map((e) => (
                    <tr key={e.sourceGpoId}>
                      <td>
                        <code>{e.sourceGpoId}</code>
                        {e.displayName ? <div className="muted">{e.displayName}</div> : null}
                      </td>
                      <td>{e.versionCount} ({e.versionLabels.join(', ')})</td>
                      <td>{e.mappingStatus}</td>
                      <td>{e.mappedControlledGpoId ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : null}
            {g.agpmReport.conflicts.length ? (
              <ul className="conflict-list">
                {g.agpmReport.conflicts.map((c, i) => (
                  <li key={`${c.gpoId}-${i}`}>
                    <strong>{c.severity ?? 'Info'}</strong> {c.gpoId}: {c.reason}
                  </li>
                ))}
              </ul>
            ) : null}
          </div>
        ) : null}
      </section>
    </>
  )
}
