import { useCallback, useEffect, useState } from 'react'
import './App.css'

const API_BASE = import.meta.env.VITE_API_BASE ?? ''

type Me = {
  identity: string
  authMode: string
  effectiveRoles: string[]
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
}

type GpoVersion = {
  versionNumber: number
  comment: string
  createdBy: string
  createdAt: string
  provenance?: string
}

type PolicyTreeNode = {
  path: string
  name: string
  value?: string
  state?: string
  children: PolicyTreeNode[]
}

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
    ...init,
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
    default: return String(status)
  }
}

function TreeNodeView({ node, depth = 0 }: { node: PolicyTreeNode; depth?: number }) {
  return (
    <li>
      <span className="tree-label" style={{ paddingLeft: depth * 12 }}>
        {node.name}
        {node.state ? <em> — {node.state}</em> : null}
        {node.value ? <code> {node.value}</code> : null}
      </span>
      {node.children?.length ? (
        <ul>
          {node.children.map((c) => (
            <TreeNodeView key={c.path} node={c} depth={depth + 1} />
          ))}
        </ul>
      ) : null}
    </li>
  )
}

export default function App() {
  const [me, setMe] = useState<Me | null>(null)
  const [domains, setDomains] = useState<Domain[]>([])
  const [gpos, setGpos] = useState<ControlledGpo[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [tree, setTree] = useState<PolicyTreeNode | null>(null)
  const [history, setHistory] = useState<GpoVersion[]>([])
  const [dnsName, setDnsName] = useState('contoso.local')
  const [netBios, setNetBios] = useState('CONTOSO')
  const [gpoId, setGpoId] = useState('{11111111-1111-1111-1111-111111111111}')
  const [gpoName, setGpoName] = useState('Demo Workstation Policy')
  const [checkInComment, setCheckInComment] = useState('')
  const [agpmPath, setAgpmPath] = useState('C:\\AGPM\\History')
  const [agpmReport, setAgpmReport] = useState<string>('')
  const [message, setMessage] = useState<string>('')
  const [error, setError] = useState<string>('')

  const refresh = useCallback(async () => {
    setError('')
    try {
      const [m, d, g] = await Promise.all([
        api<Me>('/api/me'),
        api<Domain[]>('/api/domains'),
        api<ControlledGpo[]>('/api/gpos'),
      ])
      setMe(m)
      setDomains(d)
      setGpos(g)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  async function registerDomain() {
    setError('')
    try {
      await api('/api/domains', {
        method: 'POST',
        body: JSON.stringify({ dnsName, netBiosName: netBios }),
      })
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
      })
      setMessage(`Take Control: ${gpoName}`)
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function selectGpo(id: string) {
    setSelectedId(id)
    setError('')
    try {
      const [t, h] = await Promise.all([
        api<PolicyTreeNode>(`/api/gpos/${id}/settings-tree`),
        api<GpoVersion[]>(`/api/gpos/${id}/history`),
      ])
      setTree(t)
      setHistory(h)
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
      })
      setMessage(anyway ? 'Checkout anyway completed.' : 'Checked out.')
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function checkIn() {
    if (!selectedId) return
    setError('')
    try {
      await api(`/api/gpos/${selectedId}/check-in`, {
        method: 'POST',
        body: JSON.stringify({ comment: checkInComment || 'Check-in from UI' }),
      })
      setCheckInComment('')
      setMessage('Check-in saved to history.')
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function runOobScan() {
    setError('')
    try {
      const hits = await api<unknown[]>('/api/oob/scan', { method: 'POST' })
      setMessage(`OOB scan: ${hits.length} detection(s).`)
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
      })
      setAgpmReport(JSON.stringify(report, null, 2))
      setMessage('AGPM dry-run complete (no writes).')
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  return (
    <div className="app-shell">
      <header className="hero">
        <p className="brand">Group Policy Director</p>
        <h1>Kontrollierte Group-Policy-Änderungen</h1>
        <p className="lede">
          Slice 1 — Take Control, Checkout, Historie, Delegation und OOB-Mail-Stubs.
        </p>
        {me ? (
          <p className="identity">
            {me.identity} · {me.authMode}
          </p>
        ) : null}
      </header>

      {(message || error) && (
        <div className={`banner ${error ? 'error' : 'ok'}`} role="status">
          {error || message}
        </div>
      )}

      <main className="layout">
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
        </section>

        <section className="panel">
          <h2>Kontrollierte GPOs</h2>
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
                </button>
              </li>
            ))}
          </ul>
          {selectedId ? (
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
          ) : null}
        </section>

        <section className="panel">
          <h2>Settings (read-only)</h2>
          {tree ? (
            <ul className="tree">
              <TreeNodeView node={tree} />
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
          <h2>OOB &amp; AGPM</h2>
          <div className="actions">
            <button type="button" onClick={() => void runOobScan()}>
              OOB-Scan (Mail-Stub)
            </button>
          </div>
          <div className="row">
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
        <p>API: {API_BASE} · Delegation an AD-Admin-Gruppen folgt über /api/delegation</p>
      </footer>
    </div>
  )
}
