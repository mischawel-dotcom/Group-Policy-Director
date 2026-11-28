import { useGpd } from '../gpd/GpdContext'
import { containerKindLabel, statusLabel } from '../labels'

export function ContainersView() {
  const g = useGpd()

  return (
    <>
      <section className="panel view-intro">
        <h2>Container / Links</h2>
        <p className="muted">
          OU / Domain / Site Take Control, gpLink-Editor, Checkout → Check-in. Freigabe auch unter Änderungen.
        </p>
      </section>

      <section className="panel">
        <h2>Take Control — AD Container (gpLink)</h2>
        <div className="row">
          <label>
            Typ
            <select value={g.containerKind} onChange={(e) => g.setContainerKind(Number(e.target.value))}>
              <option value={1}>OU</option>
              <option value={2}>Domain</option>
              <option value={3}>Site</option>
            </select>
          </label>
          <label>
            Container-Key (DN)
            <input value={g.containerKey} onChange={(e) => g.setContainerKey(e.target.value)} />
          </label>
          <label>
            Anzeigename
            <input value={g.containerName} onChange={(e) => g.setContainerName(e.target.value)} />
          </label>
          <button type="button" onClick={() => void g.takeControlContainer()}>
            Take Control Container
          </button>
        </div>
        <p className="muted">
          Sites / Domain / OUs: gpLink Order, Enforce, Disable, Block Inheritance — gleicher Checkout → Approve → Deploy-Pfad.
        </p>
      </section>

      <section className="panel workflow-panel">
        <h2>Kontrollierte Container &amp; Links-Editor</h2>
        <ul className="gpo-list">
          {g.containers.map((c) => (
            <li key={c.id}>
              <button
                type="button"
                className={g.selectedContainerId === c.id ? 'selected' : ''}
                onClick={() => void g.selectContainer(c.id)}
              >
                <strong>
                  {containerKindLabel(c.kind)} · {c.displayName}
                </strong>
                <span>{statusLabel(c.status)}</span>
                <span className="muted temp-id">{c.containerKey}</span>
              </button>
            </li>
          ))}
        </ul>
        {g.selectedContainerId ? (
          <>
            <div className="actions">
              <button type="button" onClick={() => void g.containerCheckout()}>
                Checkout
              </button>
              <input
                placeholder="Check-in Kommentar"
                value={g.containerCheckInComment}
                onChange={(e) => g.setContainerCheckInComment(e.target.value)}
              />
              <button type="button" onClick={() => void g.containerCheckIn()}>
                Check-in
              </button>
            </div>
            <div className="actions">
              <input
                placeholder="Approve/Reject Kommentar"
                value={g.containerReviewComment}
                onChange={(e) => g.setContainerReviewComment(e.target.value)}
              />
              <button type="button" onClick={() => void g.containerApprove()}>
                Approve
              </button>
              <button type="button" className="secondary" onClick={() => void g.containerReject()}>
                Reject
              </button>
              <button type="button" onClick={() => void g.containerDeploy()}>
                Deploy now
              </button>
            </div>
            <div className="actions">
              <label>
                Schedule
                <input
                  type="datetime-local"
                  value={g.containerScheduleAt}
                  onChange={(e) => g.setContainerScheduleAt(e.target.value)}
                />
              </label>
              <label className="checkbox-label">
                Rollback-on-fail
                <input
                  type="checkbox"
                  checked={g.containerRollbackOnFail}
                  onChange={(e) => g.setContainerRollbackOnFail(e.target.checked)}
                />
              </label>
              <label className="checkbox-label">
                Simulate fail
                <input
                  type="checkbox"
                  checked={g.containerSimulateFail}
                  onChange={(e) => g.setContainerSimulateFail(e.target.checked)}
                />
              </label>
              <button type="button" onClick={() => void g.scheduleContainerDeploy()}>
                Schedule Deploy
              </button>
              <button type="button" className="secondary" onClick={() => void g.cancelContainerSchedule()}>
                Cancel Schedule
              </button>
            </div>
          </>
        ) : (
          <p className="muted">Container Take Control, dann auswählen.</p>
        )}

        {g.containerLinks ? (
          <div className="links-editor">
            <h3>gpLink Links</h3>
            <label className="checkbox-label">
              Block Inheritance
              <input
                type="checkbox"
                checked={g.containerLinks.blockInheritance}
                disabled={!g.canEditLinks}
                onChange={(e) =>
                  g.setContainerLinks((prev) =>
                    prev ? { ...prev, blockInheritance: e.target.checked } : prev,
                  )
                }
              />
            </label>
            <ul className="links-list">
              {[...g.containerLinks.links]
                .sort((a, b) => a.order - b.order)
                .map((link, index) => (
                  <li key={`${link.gpoId}-${index}`}>
                    <div className="link-main">
                      <strong>
                        #{link.order} {link.displayName ?? link.gpoId}
                      </strong>
                      <code>{link.gpoId}</code>
                      <span className="muted">
                        {link.enforced ? 'Enforced' : 'Not enforced'}
                        {' · '}
                        {link.disabled ? 'Disabled' : 'Enabled'}
                      </span>
                    </div>
                    {g.canEditLinks ? (
                      <div className="link-actions">
                        <button type="button" className="secondary" onClick={() => g.moveLink(index, -1)}>
                          ↑
                        </button>
                        <button type="button" className="secondary" onClick={() => g.moveLink(index, 1)}>
                          ↓
                        </button>
                        <button type="button" className="secondary" onClick={() => g.toggleLinkFlag(index, 'enforced')}>
                          Enforce
                        </button>
                        <button type="button" className="secondary" onClick={() => g.toggleLinkFlag(index, 'disabled')}>
                          Disable
                        </button>
                        <button type="button" className="secondary" onClick={() => g.removeLink(index)}>
                          Remove
                        </button>
                      </div>
                    ) : null}
                  </li>
                ))}
            </ul>
            {g.canEditLinks ? (
              <div className="row">
                <label>
                  GPO GUID
                  <input value={g.newLinkGpoId} onChange={(e) => g.setNewLinkGpoId(e.target.value)} />
                </label>
                <label>
                  Anzeigename
                  <input value={g.newLinkName} onChange={(e) => g.setNewLinkName(e.target.value)} />
                </label>
                <button type="button" onClick={() => g.addLink()}>
                  Add Link
                </button>
                <button type="button" onClick={() => void g.saveContainerLinks()}>
                  Links speichern
                </button>
              </div>
            ) : (
              <p className="muted">Links editierbar nur bei Checkout durch denselben Akteur.</p>
            )}
          </div>
        ) : null}

        {g.containerHistory.length ? (
          <div className="container-history">
            <h3>Container Historie &amp; Diff</h3>
            <ol className="history">
              {g.containerHistory.map((v) => (
                <li key={`c-${v.versionNumber}-${v.createdAt}`}>
                  v{v.versionNumber} — {v.comment}
                  <span>
                    {v.createdBy} · {new Date(v.createdAt).toLocaleString('de-DE')}
                    {v.workflowState ? ` · ${v.workflowState}` : ''}
                  </span>
                </li>
              ))}
            </ol>
            <div className="row">
              <label>
                From
                <select
                  value={g.containerDiffFrom}
                  onChange={(e) => g.setContainerDiffFrom(Number(e.target.value))}
                >
                  {g.containerHistory.map((v) => (
                    <option key={`cfrom-${v.versionNumber}`} value={v.versionNumber}>
                      v{v.versionNumber}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                To
                <select
                  value={g.containerDiffTo}
                  onChange={(e) => g.setContainerDiffTo(Number(e.target.value))}
                >
                  {g.containerHistory.map((v) => (
                    <option key={`cto-${v.versionNumber}`} value={v.versionNumber}>
                      v{v.versionNumber}
                    </option>
                  ))}
                </select>
              </label>
              <button type="button" onClick={() => void g.loadContainerDiff()}>
                Links Diff
              </button>
            </div>
            {g.containerDiff ? (
              <div className="diff-panel">
                <p>
                  <strong>
                    Container v{g.containerDiff.fromVersion} → v{g.containerDiff.toVersion}
                  </strong>
                  {' · '}
                  {g.containerDiff.changes.length} Änderung(en)
                </p>
                <ul className="diff-list">
                  {g.containerDiff.changes.map((c) => (
                    <li key={`cd-${c.kind}-${c.path}`} className={`diff-${c.kind.toLowerCase()}`}>
                      <strong>{c.kind}</strong> <code>{c.path}</code>
                      <div className="diff-values">
                        {c.before != null ? <span className="before">− {c.before}</span> : null}
                        {c.after != null ? <span className="after">+ {c.after}</span> : null}
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        ) : null}
      </section>
    </>
  )
}
