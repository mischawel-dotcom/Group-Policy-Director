import { useGpd } from '../gpd/GpdContext'

export function WelcomeWizardPanel() {
  const { wizard, completeWizard, skipWizard, setShowWizard } = useGpd()
  if (!wizard) return null

  return (
    <section className="panel wizard-panel" aria-label="Welcome Wizard">
      <h2>Welcome Wizard (Product Admin)</h2>
      <p className="muted">
        Erster Start — überspringbar. Intune-Schritt sind deferred. Schritte helfen bei Domain/GPO, Delegation und Settings.
      </p>
      <ol className="wizard-steps">
        {wizard.steps.map((s) => (
          <li key={s.id} className={s.done ? 'done' : undefined}>
            <strong>{s.title}</strong>
            {s.optional ? <em className="muted"> (optional)</em> : null}
            {s.done ? <span className="ok-tag"> erledigt</span> : <span className="muted"> offen</span>}
            <p className="muted">{s.description}</p>
          </li>
        ))}
      </ol>
      <div className="actions">
        <button type="button" onClick={() => void completeWizard()}>
          Abschließen
        </button>
        <button type="button" className="secondary" onClick={() => void skipWizard()}>
          Überspringen
        </button>
        <button type="button" className="secondary" onClick={() => setShowWizard(false)}>
          Später
        </button>
      </div>
    </section>
  )
}
