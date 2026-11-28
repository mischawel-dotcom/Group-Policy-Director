import { Link } from 'react-router-dom'
import { useGpd } from '../gpd/GpdContext'
import {
  scheduleObjectKindLabel,
  scheduleStatusLabel,
  statusLabel,
} from '../labels'

export function ChangesView() {
  const g = useGpd()
  const pendingGpos = g.gpos.filter((item) => [3, 5, 6].includes(item.status))
  const pendingContainers = g.containers.filter((item) => [3, 5, 6].includes(item.status))

  return (
    <>
      <section className="panel view-intro">
        <h2>Änderungen / Freigaben</h2>
        <p className="muted">
          Waiting Approval, Approved und Scheduled — plus Deploy-Queue und OOB-Drift. SoD: Approver ≠ Editor.
        </p>
      </section>

      <section className="panel workflow-panel">
        <h2>Zur Freigabe / Deploy</h2>
        <h3>GPOs</h3>
        <ul className="gpo-list">
          {pendingGpos.length === 0 ? (
            <li className="muted">Keine GPOs in Waiting Approval / Approved / Scheduled.</li>
          ) : (
            pendingGpos.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  className={g.selectedId === item.id ? 'selected' : ''}
                  onClick={() => void g.selectGpo(item.id)}
                >
                  <strong>{item.displayName}</strong>
                  <span>{statusLabel(item.status)}</span>
                </button>
              </li>
            ))
          )}
        </ul>
        {g.selectedId && pendingGpos.some((item) => item.id === g.selectedId) ? (
          <>
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
              Settings/Checkout unter <Link to="/policies">GPOs</Link>.
            </p>
          </>
        ) : null}

        <h3>Container</h3>
        <ul className="gpo-list">
          {pendingContainers.length === 0 ? (
            <li className="muted">Keine Container in Waiting Approval / Approved / Scheduled.</li>
          ) : (
            pendingContainers.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  className={g.selectedContainerId === item.id ? 'selected' : ''}
                  onClick={() => void g.selectContainer(item.id)}
                >
                  <strong>{item.displayName}</strong>
                  <span>{statusLabel(item.status)}</span>
                </button>
              </li>
            ))
          )}
        </ul>
        {g.selectedContainerId && pendingContainers.some((item) => item.id === g.selectedContainerId) ? (
          <>
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
            <p className="muted">
              Links-Editor unter <Link to="/containers">Container</Link>.
            </p>
          </>
        ) : null}
      </section>

      <section className="panel workflow-panel">
        <h2>Deploy Queue &amp; Status</h2>
        <p className="muted">
          Geplante GPO-/Container-Deploys. Hosted Processor pollt fällige Jobs; Manual Process-due für Smoke-Tests.
        </p>
        <div className="actions">
          <button type="button" onClick={() => void g.processDueDeploys()}>
            Process due now
          </button>
        </div>
        <div className="audit-table-wrap">
          <table>
            <thead>
              <tr>
                <th>When</th>
                <th>Object</th>
                <th>By</th>
                <th>Flags</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {g.deployQueue.length === 0 ? (
                <tr>
                  <td colSpan={6} className="muted">
                    Keine geplanten Deploys.
                  </td>
                </tr>
              ) : (
                g.deployQueue.map((j) => (
                  <tr key={j.id} title={j.errorMessage ?? ''}>
                    <td>{new Date(j.scheduledFor).toLocaleString('de-DE')}</td>
                    <td>
                      <strong>
                        {scheduleObjectKindLabel(j.objectKind)} · {j.displayName}
                      </strong>
                      <div className="muted">{j.objectKey}</div>
                    </td>
                    <td>{j.scheduledBy}</td>
                    <td>
                      {j.rollbackOnFailure ? 'Rollback' : 'No rollback'}
                      {j.simulateFailure ? ' · SimulateFail' : ''}
                    </td>
                    <td>{scheduleStatusLabel(j.status)}</td>
                    <td>
                      {j.status === 1 ? (
                        <button type="button" className="secondary" onClick={() => void g.cancelQueueJob(j.id)}>
                          Cancel
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="panel">
        <h2>OOB (Mail-first)</h2>
        <div className="actions">
          <button type="button" className="secondary" onClick={() => void g.simulateDrift()}>
            Simulate live drift
          </button>
          <button type="button" onClick={() => void g.runOobScan()}>
            OOB-Scan + SMTP
          </button>
        </div>
        <p className="muted">
          Compare controlled baseline vs live fingerprint. Configurable SMTP (`Smtp:*`). No auto-reset.
        </p>
        {g.oobHits.length ? (
          <ul className="oob-list">
            {g.oobHits.slice(0, 8).map((o) => (
              <li key={o.id}>
                <strong>{new Date(o.detectedAt).toLocaleString('de-DE')}</strong>
                <span>{o.diffSummary}</span>
                <span className="muted">
                  mail={o.emailSent ? 'sent/logged' : 'no'} · {o.resolution ?? 'PendingDecision'}
                </span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="muted">Keine OOB-Detections.</p>
        )}
      </section>
    </>
  )
}
