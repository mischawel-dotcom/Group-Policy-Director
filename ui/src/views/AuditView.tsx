import { useGpd } from '../gpd/GpdContext'

export function AuditView() {
  const g = useGpd()

  return (
    <>
      <section className="panel view-intro">
        <h2>Audit / Historie</h2>
        <p className="muted">
          Filterbares Audit-Log und CSV-Export. Auditor ist read-only. Objekt-Historie bleibt bei GPO/Container.
        </p>
      </section>

      <section className="panel audit-panel">
        <h2>Audit Log</h2>
        <p className="muted">
          Filter nach Objekt, Akteur, Aktion und Zeitraum. CSV-Export für Compliance. Auditor ist read-only.
        </p>
        <div className="row">
          <label>
            Object
            <input value={g.auditObject} onChange={(e) => g.setAuditObject(e.target.value)} placeholder="GPO GUID / DN" />
          </label>
          <label>
            Actor
            <input value={g.auditActor} onChange={(e) => g.setAuditActor(e.target.value)} placeholder="CONTOSO\…" />
          </label>
          <label>
            Action
            <input value={g.auditAction} onChange={(e) => g.setAuditAction(e.target.value)} placeholder="BreakGlass / Gpo." />
          </label>
          <label>
            From
            <input type="datetime-local" value={g.auditFrom} onChange={(e) => g.setAuditFrom(e.target.value)} />
          </label>
          <label>
            To
            <input type="datetime-local" value={g.auditTo} onChange={(e) => g.setAuditTo(e.target.value)} />
          </label>
          <button
            type="button"
            onClick={() =>
              void g.refreshAudit()
                .then(() => g.setMessage('Audit refreshed.'))
                .catch((e) => g.setError(e instanceof Error ? e.message : String(e)))
            }
          >
            Filtern
          </button>
          <button type="button" className="secondary" onClick={() => void g.exportAuditCsv()}>
            Export CSV
          </button>
        </div>
        <div className="audit-table-wrap">
          <table className="audit-table">
            <thead>
              <tr>
                <th>Time</th>
                <th>Actor</th>
                <th>Action</th>
                <th>Object</th>
                <th>Outcome</th>
              </tr>
            </thead>
            <tbody>
              {g.auditEvents.length === 0 ? (
                <tr>
                  <td colSpan={5} className="muted">Keine Events (Filter anpassen oder Workflow ausführen).</td>
                </tr>
              ) : (
                g.auditEvents.map((e) => (
                  <tr key={e.id} title={e.detailJson ?? ''}>
                    <td>{new Date(e.occurredAt).toLocaleString()}</td>
                    <td>{e.actor}</td>
                    <td>{e.action}</td>
                    <td>{e.objectKey ?? '—'}</td>
                    <td>{e.outcome ?? '—'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>
    </>
  )
}
