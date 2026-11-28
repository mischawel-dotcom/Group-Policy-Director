import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useGpd } from '../gpd/GpdContext'
import { WelcomeWizardPanel } from './WelcomeWizardPanel'

const NAV = [
  { to: '/policies', label: 'GPOs', hint: 'Richtlinien' },
  { to: '/changes', label: 'Änderungen', hint: 'Freigaben & Queue' },
  { to: '/containers', label: 'Container', hint: 'Links' },
  { to: '/admin', label: 'Admin', hint: 'Delegation' },
  { to: '/audit', label: 'Audit', hint: 'Historie' },
] as const

export function AppShell() {
  const [navOpen, setNavOpen] = useState(false)
  const {
    me,
    actor,
    setActor,
    demoActors,
    general,
    message,
    error,
    showWizard,
    wizard,
    API_BASE,
  } = useGpd()

  return (
    <div className={`app-shell${navOpen ? ' nav-open' : ''}`}>
      <header className="topbar">
        <div className="topbar-brand">
          <p className="brand">Group Policy Director</p>
          <p className="topbar-tagline">Take Control → Checkout → Approve → Deploy</p>
        </div>
        <div className="topbar-meta">
          {me ? (
            <p className="identity">
              {me.identity} · {me.authMode}
              {me.isAuditor ? ' · Auditor' : ''}
              {me.canCreateGpo ? ' · GPO Creator' : ''}
              {me.breakGlassActive ? ' · Break Glass ACTIVE' : me.breakGlassEligible ? ' · Break Glass eligible' : ''}
              {general?.namingEnforcementEnabled ? ' · Naming ON' : ''}
            </p>
          ) : null}
          <div className="actor-switch">
            <label>
              Demo-Akteur (SoD)
              <select value={actor} onChange={(e) => setActor(e.target.value)}>
                {demoActors.map((a) => (
                  <option key={a} value={a}>{a}</option>
                ))}
              </select>
            </label>
            {me?.membershipPrincipals?.length ? (
              <p className="muted memberships">
                Gruppen: {me.membershipPrincipals.filter((p) => p !== me.identity).join(', ') || '—'}
              </p>
            ) : null}
          </div>
        </div>
      </header>

      <div className="shell-body">
        <button
          type="button"
          className="nav-toggle"
          aria-expanded={navOpen}
          aria-controls="gpd-side-nav"
          onClick={() => setNavOpen((open) => !open)}
        >
          Menü
        </button>
        <nav id="gpd-side-nav" className="side-nav" aria-label="Hauptnavigation">
          <ul>
            {NAV.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
                  onClick={() => setNavOpen(false)}
                >
                  <span className="nav-label">{item.label}</span>
                  <span className="nav-hint">{item.hint}</span>
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className="shell-main">
          {(message || error) && (
            <div className={`banner ${error ? 'error' : 'ok'}`} role="status">
              {error || message}
            </div>
          )}

          {showWizard && wizard ? <WelcomeWizardPanel /> : null}

          <main className="layout">
            <Outlet />
          </main>
        </div>
      </div>

      <footer>
        <p>
          API: {API_BASE || '/api (proxy)'} · Header {`X-Gpd-Actor`} · Multi-View Nav · In-Memory / ForceDryRun · kein SQL · kein Intune
        </p>
      </footer>
    </div>
  )
}
