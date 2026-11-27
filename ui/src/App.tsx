import { useCallback, useEffect, useState } from 'react'
import './App.css'

const API_BASE = import.meta.env.VITE_API_BASE ?? ''

type Me = {
  identity: string
  authMode: string
  effectiveRoles: string[]
  membershipPrincipals?: string[]
  demoActors?: string[]
}

type Domain = {
  id: string
  dnsName: string
  netBiosName: string
}

type ControlledGpo = {
  id: string
  displayName: string
  productionGpoId: string
  status: number
  checkedOutBy?: string
  tempGpoId?: string
  pendingVersionId?: string
  approvedBy?: string
  settingsHash?: string
}

type GpoVersion = {
  versionNumber: number
  comment: string
  createdBy: string
  createdAt: string
  provenance?: string
  workflowState?: string
  reviewedBy?: string
  reviewComment?: string
}

type PolicyTreeNode = {
  path: string
  name: string
  value?: string
  state?: string
  isEditable?: boolean
  isStub?: boolean
  settingType?: string
  children: PolicyTreeNode[]
}

type AdEnvironment = {
  isDomainJoined: boolean
  computerDnsDomain?: string
  computerNetBiosDomain?: string
  directoryReachable: boolean
  gpmcAvailable: boolean
  forceDryRunConfigured: boolean
  effectiveMode: number
  modeReason: string
}

type OobDetection = {
  id: string
  controlledGpoId: string
  detectedAt: string
  productionHash: string
  lastControlledHash: string
  diffSummary?: string
  emailSent: boolean
  resolution?: string
}

async function api<T>(path: string, init?: RequestInit, actor?: string): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init?.headers as Record<string, string> | undefined),
  }
  if (actor) {
    headers['X-Gpd-Actor'] = actor
  }
  const res = await fetch(`${API_BASE}${path}`, {
    credentials: 'include',
    ...init,
    headers,
  })
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error(body.error ?? `${res.status} ${res.statusText}`)
  }
  return res.json() as Promise<T>
}

function statusLabel(status: number): string {
  switch (status) {
    case 1: return 'Under Control'
    case 2: return 'Checked Out'
    case 3: return 'Waiting Approval'
    case 4: return 'Deployed'
    case 5: return 'Approved'
    default: return String(status)
  }
}

function adModeLabel(mode: number): string {
  return mode === 1 ? 'Live' : 'DryRun'
}

function TreeNodeView({
  node,
  depth = 0,
  canEdit,
  onChange,
}: {
  node: PolicyTreeNode
  depth?: number
  canEdit: boolean
  onChange: (path: string, patch: { state?: string; value?: string }) => void
}) {
  const editable = Boolean(canEdit && node.isEditable && !node.isStub)

  return (
    <li className={node.isStub ? 'tree-stub' : undefined}>
      <div className="tree-row" style={{ paddingLeft: depth * 14 }}>
        <span className="tree-label">
          {node.name}
          {node.isStub ? <em className="stub-tag"> Stub</em> : null}
        </span>
        {editable && node.settingType === 'PolicyState' ? (
          <select
            value={node.state ?? 'NotConfigured'}
            onChange={(e) => onChange(node.path, { state: e.target.value })}
            aria-label={`State for ${node.name}`}
          >
            <option value="NotConfigured">Not Configured</option>
            <option value="Enabled">Enabled</option>
            <option value="Disabled">Disabled</option>
          </select>
        ) : node.state ? (
          <em className="tree-state">{node.state}</em>
        ) : null}
        {editable && node.settingType === 'Text' ? (
          <input
            className="tree-value-input"
            value={node.value ?? ''}
            placeholder="Wert"
            onChange={(e) => onChange(node.path, { value: e.target.value })}
            aria-label={`Value for ${node.name}`}
          />
        ) : node.value ? (
          <code className="tree-value">{node.value}</code>
        ) : null}
      </div>
      {node.children?.length ? (
        <ul>
          {node.children.map((c) => (
            <TreeNodeView
              key={c.path}
              node={c}
              depth={depth + 1}
              canEdit={canEdit}
              onChange={onChange}
            />
          ))}
        </ul>
      ) : null}
    </li>
  )
}

function applyLocalPatch(
  root: PolicyTreeNode,
  path: string,
  patch: { state?: string; value?: string },
): PolicyTreeNode {
  if (root.path === path) {
    return { ...root, ...patch }
  }
  return {
    ...root,
    children: root.children.map((c) => applyLocalPatch(c, path, patch)),
  }
}

export default function App() {
  const [me, setMe] = useState<Me | null>(null)
  const [actor, setActor] = useState('CONTOSO\\gpd-admin')
  const [domains, setDomains] = useState<Domain[]>([])
  const [gpos, setGpos] = useState<ControlledGpo[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selected, setSelected] = useState<ControlledGpo | null>(null)
  const [tree, setTree] = useState<PolicyTreeNode | null>(null)
  const [history, setHistory] = useState<GpoVersion[]>([])
  const [adEnv, setAdEnv] = useState<AdEnvironment | null>(null)
  const [oobHits, setOobHits] = useState<OobDetection[]>([])
  const [dnsName, setDnsName] = useState('contoso.local')
  const [netBios, setNetBios] = useState('CONTOSO')
  const [gpoId, setGpoId] = useState('{11111111-1111-1111-1111-111111111111}')
  const [gpoName, setGpoName] = useState('Demo Workstation Policy')
  const [checkInComment, setCheckInComment] = useState('')
  const [reviewComment, setReviewComment] = useState('')
  const [agpmPath, setAgpmPath] = useState('C:\\AGPM\\History')
  const [agpmReport, setAgpmReport] = useState<string>('')
  const [message, setMessage] = useState<string>('')
  const [error, setError] = useState<string>('')
  const [pendingEdits, setPendingEdits] = useState<Record<string, { state?: string; value?: string }>>({})

  const refresh = useCallback(async () => {
    setError('')
    try {
      const [m, d, g, env, oob] = await Promise.all([
        api<Me>('/api/me', undefined, actor),
        api<Domain[]>('/api/domains', undefined, actor),
        api<ControlledGpo[]>('/api/gpos', undefined, actor),
        api<AdEnvironment>('/api/ad/environment', undefined, actor),
        api<OobDetection[]>('/api/oob', undefined, actor),
      ])
      setMe(m)
      setDomains(d)
      setGpos(g)
      setAdEnv(env)
      setOobHits(oob)
      if (selectedId) {
        const current = g.find((x) => x.id === selectedId) ?? null
        setSelected(current)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }, [actor, selectedId])

  useEffect(() => {
    void refresh()
  }, [refresh])

  async function registerDomain() {
    setError('')
    try {
      await api('/api/domains', {
        method: 'POST',
        body: JSON.stringify({ dnsName, netBiosName: netBios }),
      }, actor)
      setMessage(`Domain ${dnsName} registered.`)
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function takeControl() {
    setError('')
    const domainId = domains[0]?.id
    if (!domainId) {
      setError('Register a domain first.')
      return
    }
    try {
      await api('/api/gpos/take-control', {
        method: 'POST',
        body: JSON.stringify({
          domainId,
          productionGpoId: gpoId,
          displayName: gpoName,
        }),
      }, actor)
      const mode = adEnv ? adModeLabel(adEnv.effectiveMode) : 'DryRun'
      setMessage(`Take Control: ${gpoName} (${mode} AD path).`)
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function selectGpo(id: string) {
    setSelectedId(id)
    setPendingEdits({})
    setError('')
    try {
      const [t, h, list] = await Promise.all([
        api<PolicyTreeNode>(`/api/gpos/${id}/settings-tree`, undefined, actor),
        api<GpoVersion[]>(`/api/gpos/${id}/history`, undefined, actor),
        api<ControlledGpo[]>('/api/gpos', undefined, actor),
      ])
      setTree(t)
      setHistory(h)
      setGpos(list)
      setSelected(list.find((g) => g.id === id) ?? null)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function checkout(anyway = false) {
    if (!selectedId) return
    setError('')
    try {
      await api(`/api/gpos/${selectedId}/checkout`, {
        method: 'POST',
        body: JSON.stringify({ checkoutAnyway: anyway }),
      }, actor)
      setMessage(anyway ? 'Checkout anyway completed (Temp GPO).' : 'Checked out — Temp-GPO bereit zum Edit.')
      setPendingEdits({})
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  function onTreeChange(path: string, patch: { state?: string; value?: string }) {
    setTree((prev) => (prev ? applyLocalPatch(prev, path, patch) : prev))
    setPendingEdits((prev) => ({
      ...prev,
      [path]: { ...prev[path], ...patch },
    }))
  }

  async function saveSettings() {
    if (!selectedId) return
    const updates = Object.entries(pendingEdits).map(([path, patch]) => ({
      path,
      state: patch.state,
      value: patch.value,
    }))
    if (!updates.length) {
      setMessage('Keine Settings-Änderungen.')
      return
    }
    setError('')
    try {
      const t = await api<PolicyTreeNode>(`/api/gpos/${selectedId}/settings-tree`, {
        method: 'PUT',
        body: JSON.stringify({ updates }),
      }, actor)
      setTree(t)
      setPendingEdits({})
      setMessage(`Settings gespeichert (${updates.length}).`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function checkIn() {
    if (!selectedId) return
    setError('')
    try {
      if (Object.keys(pendingEdits).length) {
        await saveSettings()
      }
      await api(`/api/gpos/${selectedId}/check-in`, {
        method: 'POST',
        body: JSON.stringify({ comment: checkInComment || 'Check-in from UI' }),
      }, actor)
      setCheckInComment('')
      setMessage('Check-in → Waiting Approval. Als Approver freigeben.')
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function approve() {
    if (!selectedId) return
    setError('')
    try {
      await api(`/api/gpos/${selectedId}/approve`, {
        method: 'POST',
        body: JSON.stringify({ comment: reviewComment || null }),
      }, actor)
      setReviewComment('')
      setMessage('Approved — bereit für Deploy.')
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function reject() {
    if (!selectedId) return
    setError('')
    try {
      await api(`/api/gpos/${selectedId}/reject`, {
        method: 'POST',
        body: JSON.stringify({ comment: reviewComment || 'Rejected from UI' }),
      }, actor)
      setReviewComment('')
      setMessage('Rejected — zurück Under Control.')
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function deploy() {
    if (!selectedId) return
    setError('')
    try {
      await api(`/api/gpos/${selectedId}/deploy`, { method: 'POST' }, actor)
      const mode = adEnv ? adModeLabel(adEnv.effectiveMode) : 'DryRun'
      setMessage(`Deployed — Produktion aktualisiert (${mode}).`)
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function runOobScan() {
    setError('')
    try {
      const hits = await api<OobDetection[]>('/api/oob/scan', { method: 'POST' }, actor)
      setOobHits(await api<OobDetection[]>('/api/oob', undefined, actor))
      setMessage(`OOB scan: ${hits.length} new detection(s). Mail via SMTP config (no auto-reset).`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function simulateDrift() {
    if (!selected) {
      setError('Select a controlled GPO first.')
      return
    }
    setError('')
    try {
      await api('/api/ad/simulate-drift', {
        method: 'POST',
        body: JSON.stringify({ productionGpoId: selected.productionGpoId }),
      }, actor)
      setMessage(`Simulated live drift for ${selected.displayName}. Run OOB scan.`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function agpmDryRun() {
    setError('')
    try {
      const report = await api<Record<string, unknown>>('/api/agpm/import/dry-run', {
        method: 'POST',
        body: JSON.stringify({ archivePath: agpmPath, printOnly: true }),
      }, actor)
      setAgpmReport(JSON.stringify(report, null, 2))
      setMessage('AGPM dry-run complete (no writes).')
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  const canEditTree = selected?.status === 2 && selected.checkedOutBy?.toLowerCase() === actor.toLowerCase()
  const demoActors = me?.demoActors ?? [
    'CONTOSO\\gpd-admin',
    'CONTOSO\\gpd-editor',
    'CONTOSO\\gpd-approver',
    'CONTOSO\\gpd-deployer',
  ]

  return (
    <div className="app-shell">
      <header className="hero">
        <p className="brand">Group Policy Director</p>
        <h1>Take Control → Checkout → Approve → Deploy</h1>
        <p className="lede">
          Slice 3 — AD Temp-GPO path with domain detection, OOB baseline-vs-live + SMTP, wired into the Slice 2 workflow.
        </p>
        {me ? (
          <p className="identity">
            {me.identity} · {me.authMode}
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
      </header>

      {(message || error) && (
        <div className={`banner ${error ? 'error' : 'ok'}`} role="status">
          {error || message}
        </div>
      )}

      <main className="layout">
        <section className="panel">
          <h2>AD / GPMC Environment</h2>
          {adEnv ? (
            <ul className="plain env-list">
              <li>
                Mode: <strong>{adModeLabel(adEnv.effectiveMode)}</strong>
                {adEnv.forceDryRunConfigured ? ' (ForceDryRun)' : ''}
              </li>
              <li>Domain-joined: {adEnv.isDomainJoined ? 'yes' : 'no'}</li>
              <li>Directory reachable: {adEnv.directoryReachable ? 'yes' : 'no'}</li>
              <li>GPMC detected: {adEnv.gpmcAvailable ? 'yes' : 'no'}</li>
              {adEnv.computerDnsDomain ? <li>DNS domain: {adEnv.computerDnsDomain}</li> : null}
              <li className="muted">{adEnv.modeReason}</li>
            </ul>
          ) : (
            <p className="muted">Loading environment…</p>
          )}
        </section>

        <section className="panel">
          <h2>Domain</h2>
          <div className="row">
            <label>
              DNS
              <input value={dnsName} onChange={(e) => setDnsName(e.target.value)} />
            </label>
            <label>
              NetBIOS
              <input value={netBios} onChange={(e) => setNetBios(e.target.value)} />
            </label>
            <button type="button" onClick={() => void registerDomain()}>
              Registrieren
            </button>
          </div>
          <ul className="plain">
            {domains.map((d) => (
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
              <input value={gpoId} onChange={(e) => setGpoId(e.target.value)} />
            </label>
            <label>
              Anzeigename
              <input value={gpoName} onChange={(e) => setGpoName(e.target.value)} />
            </label>
            <button type="button" onClick={() => void takeControl()}>
              Take Control
            </button>
          </div>
          <p className="muted">
            Live: LDAP ACL lockdown plan/bind. Offline: dry-run steps + baseline seed. Delegation-Seed: GPD-Editors / Approvers / Deployers.
          </p>
        </section>

        <section className="panel workflow-panel">
          <h2>Kontrollierte GPOs &amp; Workflow</h2>
          <ul className="gpo-list">
            {gpos.map((g) => (
              <li key={g.id}>
                <button
                  type="button"
                  className={selectedId === g.id ? 'selected' : ''}
                  onClick={() => void selectGpo(g.id)}
                >
                  <strong>{g.displayName}</strong>
                  <span>{statusLabel(g.status)}</span>
                  {g.tempGpoId ? <span className="muted temp-id">{g.tempGpoId}</span> : null}
                </button>
              </li>
            ))}
          </ul>
          {selectedId ? (
            <>
              <div className="actions">
                <button type="button" onClick={() => void checkout(false)}>
                  Checkout
                </button>
                <button type="button" className="secondary" onClick={() => void checkout(true)}>
                  Checkout anyway
                </button>
                <input
                  placeholder="Check-in Kommentar"
                  value={checkInComment}
                  onChange={(e) => setCheckInComment(e.target.value)}
                />
                <button type="button" onClick={() => void checkIn()}>
                  Check-in
                </button>
              </div>
              <div className="actions">
                <input
                  placeholder="Approve/Reject Kommentar"
                  value={reviewComment}
                  onChange={(e) => setReviewComment(e.target.value)}
                />
                <button type="button" onClick={() => void approve()}>
                  Approve
                </button>
                <button type="button" className="secondary" onClick={() => void reject()}>
                  Reject
                </button>
                <button type="button" onClick={() => void deploy()}>
                  Deploy
                </button>
              </div>
              <p className="muted">
                SoD: Editor ≠ Approver/Deployer. Temp-GPO id stays until Deploy/Reject.
              </p>
            </>
          ) : null}
        </section>

        <section className="panel settings-panel">
          <h2>GPO Settings Tree</h2>
          {canEditTree ? (
            <div className="actions">
              <button type="button" onClick={() => void saveSettings()}>
                Settings speichern
              </button>
              <span className="muted">
                {Object.keys(pendingEdits).length
                  ? `${Object.keys(pendingEdits).length} Änderung(en) lokal`
                  : 'Keine offenen Änderungen'}
              </span>
            </div>
          ) : (
            <p className="muted">
              Edit nur bei Checkout durch denselben Akteur. Stubs = ohne ADMX/GPMC-Agent.
            </p>
          )}
          {tree ? (
            <ul className="tree">
              <TreeNodeView node={tree} canEdit={canEditTree} onChange={onTreeChange} />
            </ul>
          ) : (
            <p className="muted">GPO auswählen, um den Policy-Tree zu laden.</p>
          )}
        </section>

        <section className="panel">
          <h2>Historie</h2>
          {history.length ? (
            <ol className="history">
              {history.map((v) => (
                <li key={`${v.versionNumber}-${v.createdAt}`}>
                  v{v.versionNumber} — {v.comment}
                  <span>
                    {v.createdBy} · {new Date(v.createdAt).toLocaleString('de-DE')}
                    {v.workflowState ? ` · ${v.workflowState}` : ''}
                    {v.reviewedBy ? ` · review: ${v.reviewedBy}` : ''}
                    {v.provenance ? ` · ${v.provenance}` : ''}
                  </span>
                </li>
              ))}
            </ol>
          ) : (
            <p className="muted">Keine Versionen.</p>
          )}
        </section>

        <section className="panel">
          <h2>OOB (Mail-first)</h2>
          <div className="actions">
            <button type="button" className="secondary" onClick={() => void simulateDrift()}>
              Simulate live drift
            </button>
            <button type="button" onClick={() => void runOobScan()}>
              OOB-Scan + SMTP
            </button>
          </div>
          <p className="muted">
            Compare controlled baseline vs live fingerprint. Configurable SMTP (`Smtp:*`). No auto-reset.
          </p>
          {oobHits.length ? (
            <ul className="oob-list">
              {oobHits.slice(0, 8).map((o) => (
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
          <div className="row" style={{ marginTop: '1rem' }}>
            <label>
              AGPM-Archivpfad
              <input value={agpmPath} onChange={(e) => setAgpmPath(e.target.value)} />
            </label>
            <button type="button" onClick={() => void agpmDryRun()}>
              AGPM Dry-Run
            </button>
          </div>
          {agpmReport ? <pre className="report">{agpmReport}</pre> : null}
        </section>
      </main>

      <footer>
        <p>
          API: {API_BASE || '/api (proxy)'} · Header {`X-Gpd-Actor`} · Slice 3 AD/OOB · keine Tier-Gates
        </p>
      </footer>
    </div>
  )
}
