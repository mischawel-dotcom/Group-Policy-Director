import { TreeNodeView } from '../components/TreeNodeView'
import { useGpd } from '../gpd/GpdContext'
import { statusLabel } from '../labels'

export function PoliciesView() {
  const g = useGpd()

  return (
    <>
      <section className="panel view-intro">
        <h2>GPOs / Richtlinien</h2>
        <p className="muted">
          Domain registrieren, Take Control, Create/Rename, Checkout → Settings → Check-in. Freigabe und Deploy unter Änderungen.
        </p>
      </section>

      <section className="panel">
        <h2>Domain</h2>
        <div className="row">
          <label>
            DNS
            <input value={g.dnsName} onChange={(e) => g.setDnsName(e.target.value)} />
          </label>
          <label>
            NetBIOS
            <input value={g.netBios} onChange={(e) => g.setNetBios(e.target.value)} />
          </label>
          <button type="button" onClick={() => void g.registerDomain()}>
            Registrieren
          </button>
        </div>
        <ul className="plain">
          {g.domains.map((d) => (
            <li key={d.id}>
              {d.dnsName} ({d.netBiosName})
            </li>
          ))}
        </ul>
      </section>

      <section className="panel">
        <h2>Take Control</h2>
        <div className="row">
          <label>
            GPO GUID
            <input value={g.gpoId} onChange={(e) => g.setGpoId(e.target.value)} />
          </label>
          <label>
            Anzeigename
            <input value={g.gpoName} onChange={(e) => g.setGpoName(e.target.value)} />
          </label>
          <button type="button" onClick={() => void g.takeControl()}>
            Take Control
          </button>
        </div>
        <p className="muted">
          Live: LDAP ACL lockdown plan/bind. Offline: dry-run steps + baseline seed. Delegation-Seed: GPD-Editors / Approvers / Deployers.
        </p>
      </section>

      <section className="panel">
        <h2>Create / Rename GPO</h2>
        <p className="muted">
          Create erfordert GpoCreator oder Product Admin. Rename erfordert Editor. Naming-Regex greift bei Create und Rename, wenn aktiv.
        </p>
        <div className="row">
          <label>
            Neuer Name
            <input value={g.createName} onChange={(e) => g.setCreateName(e.target.value)} />
          </label>
          <label>
            Copy from (optional Id)
            <select value={g.createCopyFrom} onChange={(e) => g.setCreateCopyFrom(e.target.value)}>
              <option value="">— leer —</option>
              {g.gpos.map((item) => (
                <option key={item.id} value={item.id}>{item.displayName}</option>
              ))}
            </select>
          </label>
          <label className="checkbox-label">
            Checkout danach
            <input
              type="checkbox"
              checked={g.createCheckout}
              onChange={(e) => g.setCreateCheckout(e.target.checked)}
            />
          </label>
          <button type="button" onClick={() => void g.createGpo()} disabled={!g.me?.canCreateGpo}>
            Create GPO
          </button>
        </div>
        <div className="row">
          <label>
            Rename
            <input
              value={g.renameName}
              onChange={(e) => g.setRenameName(e.target.value)}
              disabled={!g.selectedId}
              placeholder="Anzeigename"
            />
          </label>
          <button type="button" onClick={() => void g.renameGpo()} disabled={!g.selectedId}>
            Rename
          </button>
        </div>
      </section>

      <section className="panel">
        <h2>General Settings — GPO Naming</h2>
        <div className="row">
          <label>
            Naming Regex
            <input
              value={g.namingRegex}
              onChange={(e) => g.setNamingRegex(e.target.value)}
              placeholder="z.B. ^Policy_\w+-\w+-\d+$"
            />
          </label>
          <button type="button" onClick={() => void g.saveNaming()}>
            Speichern
          </button>
        </div>
        <p className="muted">
          {g.general?.namingEnforcementEnabled
            ? `Aktiv: ${g.general.gpoNamingRegex}`
            : 'Leer = keine Enforcement. Beispiele wie Referenz-Tools: Policy_\\w*-\\w*-\\d*'}
        </p>
      </section>

      <section className="panel workflow-panel">
        <h2>Kontrollierte GPOs &amp; Workflow</h2>
        <ul className="gpo-list">
          {g.gpos.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                className={g.selectedId === item.id ? 'selected' : ''}
                onClick={() => void g.selectGpo(item.id)}
              >
                <strong>{item.displayName}</strong>
                <span>{statusLabel(item.status)}</span>
                {item.tempGpoId ? <span className="muted temp-id">{item.tempGpoId}</span> : null}
              </button>
            </li>
          ))}
        </ul>
        {g.selectedId ? (
          <>
            <div className="actions">
              <button type="button" onClick={() => void g.checkout(false)}>
                Checkout
              </button>
              <button type="button" className="secondary" onClick={() => void g.checkout(true)}>
                Checkout anyway
              </button>
              <input
                placeholder="Check-in Kommentar"
                value={g.checkInComment}
                onChange={(e) => g.setCheckInComment(e.target.value)}
              />
              <button type="button" onClick={() => void g.checkIn()}>
                Check-in
              </button>
            </div>
            <div className="actions">
              <input
                placeholder="Approve/Reject Kommentar"
                value={g.reviewComment}
                onChange={(e) => g.setReviewComment(e.target.value)}
              />
              <button type="button" onClick={() => void g.approve()}>
                Approve
              </button>
              <button type="button" className="secondary" onClick={() => void g.reject()}>
                Reject
              </button>
              <button type="button" onClick={() => void g.deploy()}>
                Deploy now
              </button>
            </div>
            <div className="actions">
              <label>
                Schedule
                <input
                  type="datetime-local"
                  value={g.scheduleAt}
                  onChange={(e) => g.setScheduleAt(e.target.value)}
                />
              </label>
              <label className="checkbox-label">
                Rollback-on-fail
                <input
                  type="checkbox"
                  checked={g.rollbackOnFail}
                  onChange={(e) => g.setRollbackOnFail(e.target.checked)}
                />
              </label>
              <label className="checkbox-label">
                Simulate fail
                <input
                  type="checkbox"
                  checked={g.simulateFail}
                  onChange={(e) => g.setSimulateFail(e.target.checked)}
                />
              </label>
              <button type="button" onClick={() => void g.scheduleDeploy()}>
                Schedule Deploy
              </button>
              <button type="button" className="secondary" onClick={() => void g.cancelGpoSchedule()}>
                Cancel Schedule
              </button>
            </div>
            <p className="muted">
              SoD: Editor ≠ Approver/Deployer. Temp-GPO id stays until Deploy/Reject. Status Scheduled = in Queue.
            </p>
          </>
        ) : null}
      </section>

      <section className="panel settings-panel">
        <h2>GPO Settings Tree</h2>
        {g.canEditTree ? (
          <div className="actions">
            <button type="button" onClick={() => void g.saveSettings()}>
              Settings speichern
            </button>
            <span className="muted">
              {Object.keys(g.pendingEdits).length
                ? `${Object.keys(g.pendingEdits).length} Änderung(en) lokal`
                : 'Keine offenen Änderungen'}
            </span>
          </div>
        ) : (
          <p className="muted">
            Edit nur bei Checkout durch denselben Akteur. Stubs = ohne ADMX/GPMC-Agent.
          </p>
        )}
        {g.tree ? (
          <ul className="tree">
            <TreeNodeView node={g.tree} canEdit={g.canEditTree} onChange={g.onTreeChange} />
          </ul>
        ) : (
          <p className="muted">GPO auswählen, um den Policy-Tree zu laden.</p>
        )}
      </section>

      <section className="panel history-panel">
        <h2>Historie &amp; Diff (GPO)</h2>
        {g.history.length ? (
          <>
            <ol className="history">
              {g.history.map((v) => (
                <li key={`${v.versionNumber}-${v.createdAt}`}>
                  <button
                    type="button"
                    className="history-pick"
                    onClick={() => {
                      g.setDiffFrom((prev) => (prev === v.versionNumber ? prev : Math.min(prev, v.versionNumber)))
                      g.setDiffTo(v.versionNumber)
                    }}
                  >
                    v{v.versionNumber} — {v.comment}
                  </button>
                  <span>
                    {v.createdBy} · {new Date(v.createdAt).toLocaleString('de-DE')}
                    {v.workflowState ? ` · ${v.workflowState}` : ''}
                    {v.reviewedBy ? ` · review: ${v.reviewedBy}` : ''}
                    {v.provenance ? ` · ${v.provenance}` : ''}
                    {v.settingsHash ? ` · hash ${v.settingsHash.slice(0, 10)}…` : ''}
                  </span>
                </li>
              ))}
            </ol>
            <div className="row">
              <label>
                From
                <select value={g.diffFrom} onChange={(e) => g.setDiffFrom(Number(e.target.value))}>
                  {g.history.map((v) => (
                    <option key={`from-${v.versionNumber}`} value={v.versionNumber}>
                      v{v.versionNumber}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                To
                <select value={g.diffTo} onChange={(e) => g.setDiffTo(Number(e.target.value))}>
                  {g.history.map((v) => (
                    <option key={`to-${v.versionNumber}`} value={v.versionNumber}>
                      v{v.versionNumber}
                    </option>
                  ))}
                </select>
              </label>
              <button type="button" onClick={() => void g.loadGpoDiff()}>
                Settings Diff
              </button>
            </div>
            {g.diffResult ? (
              <div className="diff-panel">
                <p>
                  <strong>
                    {g.diffResult.objectType} v{g.diffResult.fromVersion} → v{g.diffResult.toVersion}
                  </strong>
                  {' · '}
                  {g.diffResult.changes.length} Änderung(en)
                </p>
                {g.diffResult.changes.length ? (
                  <ul className="diff-list">
                    {g.diffResult.changes.map((c) => (
                      <li key={`${c.kind}-${c.path}`} className={`diff-${c.kind.toLowerCase()}`}>
                        <strong>{c.kind}</strong> <code>{c.path}</code>
                        <div className="diff-values">
                          {c.before != null ? <span className="before">− {c.before}</span> : null}
                          {c.after != null ? <span className="after">+ {c.after}</span> : null}
                        </div>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="muted">Keine Settings-Unterschiede.</p>
                )}
              </div>
            ) : null}
          </>
        ) : (
          <p className="muted">Keine Versionen.</p>
        )}
      </section>
    </>
  )
}
