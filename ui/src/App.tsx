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

type ProductRoleAssignment = {
  id: string
  role: number
  trusteePrincipal: string
  grantedBy: string
  grantedAt: string
}

type DelegationGrant = {
  id: string
  scopeKind: number
  scopeKey: string
  role: number
  trusteePrincipal: string
  inheritToChildren: boolean
  grantedBy: string
  grantedAt: string
}

type SuggestedTrustee = {
  principal: string
  kind: string
  description?: string
}

type ScopeHint = {
  scopeKind: number
  scopeKey: string
  label?: string
}

type AgpmEntry = {
  sourceGpoId: string
  displayName?: string
  versionCount: number
  versionLabels: string[]
  mappedControlledGpoId?: string
  mappingStatus: string
  notes?: string
}

type AgpmConflict = {
  gpoId: string
  reason: string
  severity?: string
}

type AgpmReport = {
  archivePath: string
  archiveFileName?: string
  parser: string
  archivesScanned: number
  versionsFound: number
  mappableGpos: number
  unmappedGpos: number
  entries: AgpmEntry[]
  conflicts: AgpmConflict[]
  notes: string
}

async function api<T>(path: string, init?: RequestInit, actor?: string): Promise<T> {
  const headers: Record<string, string> = {
    ...(init?.headers as Record<string, string> | undefined),
  }
  if (!(init?.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json'
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
  if (res.status === 204) {
    return undefined as T
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

function productRoleLabel(role: number): string {
  switch (role) {
    case 1: return 'ProductAdministrator'
    case 2: return 'GpoCreator'
    case 3: return 'BreakGlass'
    case 4: return 'Auditor'
    default: return String(role)
  }
}

function objectRoleLabel(role: number): string {
  switch (role) {
    case 1: return 'Editor'
    case 2: return 'Approver'
    case 3: return 'Deployer'
    default: return String(role)
  }
}

function scopeKindLabel(kind: number): string {
  return kind === 1 ? 'OU' : 'GPO'
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
  const [agpmPath, setAgpmPath] = useState('')
  const [agpmReport, setAgpmReport] = useState<AgpmReport | null>(null)
  const [agpmFile, setAgpmFile] = useState<File | null>(null)
  const [productRoles, setProductRoles] = useState<ProductRoleAssignment[]>([])
  const [objectRoles, setObjectRoles] = useState<DelegationGrant[]>([])
  const [trustees, setTrustees] = useState<SuggestedTrustee[]>([])
  const [scopeHints, setScopeHints] = useState<ScopeHint[]>([])
  const [delScopeKind, setDelScopeKind] = useState(1)
  const [delScopeKey, setDelScopeKey] = useState('OU=Workstations,DC=contoso,DC=local')
  const [delRole, setDelRole] = useState(1)
  const [delTrustee, setDelTrustee] = useState('CONTOSO\\GPD-Editors')
  const [delInherit, setDelInherit] = useState(true)
  const [prodRole, setProdRole] = useState(2)
  const [prodTrustee, setProdTrustee] = useState('CONTOSO\\GPD-ProductAdmins')
  const [message, setMessage] = useState<string>('')
  const [error, setError] = useState<string>('')
  const [pendingEdits, setPendingEdits] = useState<Record<string, { state?: string; value?: string }>>({})

  const refreshDelegation = useCallback(async () => {
    const [pr, oroles, sug, hints] = await Promise.all([
      api<ProductRoleAssignment[]>('/api/delegation/product-roles', undefined, actor),
      api<DelegationGrant[]>('/api/delegation/object-roles', undefined, actor),
      api<SuggestedTrustee[]>('/api/delegation/suggested-trustees', undefined, actor),
      api<ScopeHint[]>('/api/delegation/scope-hints', undefined, actor),
    ])
    setProductRoles(pr)
    setObjectRoles(oroles)
    setTrustees(sug)
    setScopeHints(hints)
  }, [actor])

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
      await refreshDelegation()
      const fixture = await api<{ available: boolean; path?: string }>('/api/agpm/fixture', undefined, actor)
      if (fixture.available && fixture.path) {
        setAgpmPath((prev) => prev || fixture.path || '')
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }, [actor, selectedId, refreshDelegation])

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

  async function agpmDryRunPath() {
    setError('')
    try {
      const report = await api<AgpmReport>('/api/agpm/import/dry-run', {
        method: 'POST',
        body: JSON.stringify({ archivePath: agpmPath, printOnly: true }),
      }, actor)
      setAgpmReport(report)
      setMessage(`AGPM dry-run: ${report.mappableGpos} mappable / ${report.unmappedGpos} unmapped.`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function agpmDryRunUpload() {
    if (!agpmFile) {
      setError('Choose a .zip archive (fixture OK).')
      return
    }
    setError('')
    try {
      const form = new FormData()
      form.append('archive', agpmFile)
      form.append('printOnly', 'true')
      const report = await api<AgpmReport>('/api/agpm/import/dry-run/upload', {
        method: 'POST',
        body: form,
      }, actor)
      setAgpmReport(report)
      setMessage(`AGPM upload dry-run: ${report.versionsFound} version(s) in ${report.entries.length} GPO(s).`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function grantObjectRole() {
    setError('')
    try {
      await api('/api/delegation/object-roles', {
        method: 'POST',
        body: JSON.stringify({
          scopeKind: delScopeKind,
          scopeKey: delScopeKey,
          role: delRole,
          trusteePrincipal: delTrustee,
          inheritToChildren: delInherit,
        }),
      }, actor)
      setMessage(`Granted ${objectRoleLabel(delRole)} on ${scopeKindLabel(delScopeKind)} to ${delTrustee}.`)
      await refreshDelegation()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function revokeObjectRole(id: string) {
    setError('')
    try {
      await api(`/api/delegation/object-roles/${id}`, { method: 'DELETE' }, actor)
      setMessage('Object role revoked.')
      await refreshDelegation()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function grantProductRole() {
    setError('')
    try {
      await api('/api/delegation/product-roles', {
        method: 'POST',
        body: JSON.stringify({ role: prodRole, trusteePrincipal: prodTrustee }),
      }, actor)
      setMessage(`Granted product role ${productRoleLabel(prodRole)} to ${prodTrustee}.`)
      await refreshDelegation()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function revokeProductRole(id: string) {
    setError('')
    try {
      await api(`/api/delegation/product-roles/${id}`, { method: 'DELETE' }, actor)
      setMessage('Product role revoked.')
      await refreshDelegation()
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
          Slice 4 — AGPM archive dry-run report + Admin-Delegation an AD-Rollengruppen. Slice 3 AD/OOB bleibt erhalten.
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

        <section className="panel delegation-panel">
          <h2>Delegation (OU / GPO → AD-Gruppen)</h2>
          <p className="muted">
            Product Admin vergibt Objektrollen an bestehende AD-Admin-Rollengruppen (Referenz-Tools-Modell). SoD: Editor ≠ Approver/Deployer.
          </p>

          <h3>Objektrollen</h3>
          <div className="row">
            <label>
              Scope
              <select
                value={delScopeKind}
                onChange={(e) => {
                  const kind = Number(e.target.value)
                  setDelScopeKind(kind)
                  const hint = scopeHints.find((h) => h.scopeKind === kind)
                  if (hint) setDelScopeKey(hint.scopeKey)
                }}
              >
                <option value={1}>OU / Container</option>
                <option value={2}>GPO</option>
              </select>
            </label>
            <label>
              Scope-Key
              <input
                list="scope-hints"
                value={delScopeKey}
                onChange={(e) => setDelScopeKey(e.target.value)}
              />
              <datalist id="scope-hints">
                {scopeHints
                  .filter((h) => h.scopeKind === delScopeKind)
                  .map((h) => (
                    <option key={`${h.scopeKind}-${h.scopeKey}`} value={h.scopeKey}>
                      {h.label ?? h.scopeKey}
                    </option>
                  ))}
              </datalist>
            </label>
            <label>
              Rolle
              <select value={delRole} onChange={(e) => setDelRole(Number(e.target.value))}>
                <option value={1}>Editor</option>
                <option value={2}>Approver</option>
                <option value={3}>Deployer</option>
              </select>
            </label>
            <label>
              Trustee (AD-Gruppe)
              <input
                list="trustee-hints"
                value={delTrustee}
                onChange={(e) => setDelTrustee(e.target.value)}
              />
              <datalist id="trustee-hints">
                {trustees.map((t) => (
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
                checked={delInherit}
                onChange={(e) => setDelInherit(e.target.checked)}
              />
            </label>
            <button type="button" onClick={() => void grantObjectRole()}>
              Gewähren
            </button>
          </div>
          <ul className="delegation-list">
            {objectRoles.map((g) => (
              <li key={g.id}>
                <span>
                  <strong>{scopeKindLabel(g.scopeKind)}</strong> {g.scopeKey}
                  {' · '}
                  {objectRoleLabel(g.role)} → {g.trusteePrincipal}
                  {g.inheritToChildren ? ' (inherit)' : ''}
                </span>
                <button type="button" className="secondary" onClick={() => void revokeObjectRole(g.id)}>
                  Revoke
                </button>
              </li>
            ))}
          </ul>

          <h3>Produktrollen</h3>
          <div className="row">
            <label>
              Rolle
              <select value={prodRole} onChange={(e) => setProdRole(Number(e.target.value))}>
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
                value={prodTrustee}
                onChange={(e) => setProdTrustee(e.target.value)}
              />
            </label>
            <button type="button" onClick={() => void grantProductRole()}>
              Gewähren
            </button>
          </div>
          <ul className="delegation-list">
            {productRoles.map((p) => (
              <li key={p.id}>
                <span>
                  {productRoleLabel(p.role)} → {p.trusteePrincipal}
                  <span className="muted"> · by {p.grantedBy}</span>
                </span>
                <button type="button" className="secondary" onClick={() => void revokeProductRole(p.id)}>
                  Revoke
                </button>
              </li>
            ))}
          </ul>
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
        </section>

        <section className="panel agpm-panel">
          <h2>AGPM Import Dry-Run</h2>
          <p className="muted">
            Archive-Parser (GUID-Ordner, Backup.xml, manifest.json, .zip). Kein History-Write — nur Report.
          </p>
          <div className="row">
            <label>
              Archivpfad (Ordner oder .zip)
              <input value={agpmPath} onChange={(e) => setAgpmPath(e.target.value)} />
            </label>
            <button type="button" onClick={() => void agpmDryRunPath()}>
              Dry-Run Pfad
            </button>
          </div>
          <div className="row">
            <label>
              Fixture / Archive Upload (.zip)
              <input
                type="file"
                accept=".zip,application/zip"
                onChange={(e) => setAgpmFile(e.target.files?.[0] ?? null)}
              />
            </label>
            <button type="button" onClick={() => void agpmDryRunUpload()}>
              Dry-Run Upload
            </button>
          </div>
          {agpmReport ? (
            <div className="agpm-report">
              <p>
                <strong>{agpmReport.parser}</strong>
                {' · '}
                scanned={agpmReport.archivesScanned}
                {' · '}
                versions={agpmReport.versionsFound}
                {' · '}
                mappable={agpmReport.mappableGpos}
                {' · '}
                unmapped={agpmReport.unmappedGpos}
              </p>
              <p className="muted">{agpmReport.notes}</p>
              {agpmReport.entries.length ? (
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
                    {agpmReport.entries.map((e) => (
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
              {agpmReport.conflicts.length ? (
                <ul className="conflict-list">
                  {agpmReport.conflicts.map((c, i) => (
                    <li key={`${c.gpoId}-${i}`}>
                      <strong>{c.severity ?? 'Info'}</strong> {c.gpoId}: {c.reason}
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>
          ) : null}
        </section>
      </main>

      <footer>
        <p>
          API: {API_BASE || '/api (proxy)'} · Header {`X-Gpd-Actor`} · Slice 4 AGPM/Delegation · Referenz-Tools branding · keine Tier-Gates
        </p>
      </footer>
    </div>
  )
}
