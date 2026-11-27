import { useCallback, useEffect, useState } from 'react'
import './App.css'

const API_BASE = import.meta.env.VITE_API_BASE ?? ''

type BreakGlassSession = {
  id: string
  trusteePrincipal: string
  activatedBy: string
  activatedAt: string
  expiresAt: string
  reason: string
  revokedAt?: string
  revokedBy?: string
  isActive?: boolean
}

type AuditEvent = {
  id: string
  occurredAt: string
  actor: string
  action: string
  objectKey?: string
  outcome?: string
  detailJson?: string
}

type Me = {
  identity: string
  authMode: string
  effectiveRoles: string[]
  membershipPrincipals?: string[]
  isAuditor?: boolean
  isProductAdministrator?: boolean
  breakGlassEligible?: boolean
  breakGlassActive?: boolean
  activeBreakGlassSession?: BreakGlassSession | null
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
  settingsHash?: string
}

type ControlledContainer = {
  id: string
  displayName: string
  containerKey: string
  kind: number
  status: number
  checkedOutBy?: string
  pendingVersionId?: string
  approvedBy?: string
  linksHash?: string
}

type GpoLinkEntry = {
  gpoId: string
  displayName?: string
  order: number
  enforced: boolean
  disabled: boolean
}

type ContainerLinksState = {
  blockInheritance: boolean
  links: GpoLinkEntry[]
}

type ContainerVersion = {
  versionNumber: number
  comment: string
  createdBy: string
  createdAt: string
  provenance?: string
  workflowState?: string
  reviewedBy?: string
  reviewComment?: string
  linksHash?: string
}

type DiffChange = {
  path: string
  kind: string
  before?: string
  after?: string
}

type VersionDiffResult = {
  fromVersion: number
  toVersion: number
  objectKey: string
  objectType: string
  changes: DiffChange[]
}

type SmtpSettings = {
  host: string
  port: number
  from: string
  useSsl: boolean
  user: string
  passwordConfigured: boolean
  useDefaultCredentials: boolean
  isConfigured: boolean
  alertRecipients: string[]
  mode: string
  autoResetEnabled: boolean
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
  switch (kind) {
    case 1: return 'OU'
    case 2: return 'GPO'
    case 3: return 'Container'
    default: return String(kind)
  }
}

function containerKindLabel(kind: number): string {
  switch (kind) {
    case 1: return 'OU'
    case 2: return 'Domain'
    case 3: return 'Site'
    default: return String(kind)
  }
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
  const [diffFrom, setDiffFrom] = useState(1)
  const [diffTo, setDiffTo] = useState(1)
  const [diffResult, setDiffResult] = useState<VersionDiffResult | null>(null)
  const [containers, setContainers] = useState<ControlledContainer[]>([])
  const [selectedContainerId, setSelectedContainerId] = useState<string | null>(null)
  const [selectedContainer, setSelectedContainer] = useState<ControlledContainer | null>(null)
  const [containerLinks, setContainerLinks] = useState<ContainerLinksState | null>(null)
  const [containerHistory, setContainerHistory] = useState<ContainerVersion[]>([])
  const [containerDiffFrom, setContainerDiffFrom] = useState(1)
  const [containerDiffTo, setContainerDiffTo] = useState(1)
  const [containerDiff, setContainerDiff] = useState<VersionDiffResult | null>(null)
  const [containerKind, setContainerKind] = useState(1)
  const [containerKey, setContainerKey] = useState('OU=Workstations,DC=contoso,DC=local')
  const [containerName, setContainerName] = useState('Workstations OU')
  const [containerCheckInComment, setContainerCheckInComment] = useState('')
  const [containerReviewComment, setContainerReviewComment] = useState('')
  const [newLinkGpoId, setNewLinkGpoId] = useState('{33333333-3333-3333-3333-333333333333}')
  const [newLinkName, setNewLinkName] = useState('Extra Linked Policy')
  const [smtp, setSmtp] = useState<SmtpSettings | null>(null)
  const [smtpHost, setSmtpHost] = useState('')
  const [smtpPort, setSmtpPort] = useState(25)
  const [smtpFrom, setSmtpFrom] = useState('')
  const [smtpUseSsl, setSmtpUseSsl] = useState(false)
  const [smtpUser, setSmtpUser] = useState('')
  const [smtpPassword, setSmtpPassword] = useState('')
  const [smtpRecipients, setSmtpRecipients] = useState('gpd-admins@contoso.local')
  const [smtpTestTo, setSmtpTestTo] = useState('')
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
  const [bgSessions, setBgSessions] = useState<BreakGlassSession[]>([])
  const [bgTrustee, setBgTrustee] = useState('CONTOSO\\GPD-BreakGlass')
  const [bgReason, setBgReason] = useState('Emergency SoD bypass for production incident')
  const [bgMinutes, setBgMinutes] = useState(60)
  const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([])
  const [auditObject, setAuditObject] = useState('')
  const [auditActor, setAuditActor] = useState('')
  const [auditAction, setAuditAction] = useState('')
  const [auditFrom, setAuditFrom] = useState('')
  const [auditTo, setAuditTo] = useState('')
  const [message, setMessage] = useState<string>('')
  const [error, setError] = useState<string>('')
  const [pendingEdits, setPendingEdits] = useState<Record<string, { state?: string; value?: string }>>({})

  const refreshDelegation = useCallback(async () => {
    const [pr, oroles, sug, hints, sessions] = await Promise.all([
      api<ProductRoleAssignment[]>('/api/delegation/product-roles', undefined, actor),
      api<DelegationGrant[]>('/api/delegation/object-roles', undefined, actor),
      api<SuggestedTrustee[]>('/api/delegation/suggested-trustees', undefined, actor),
      api<ScopeHint[]>('/api/delegation/scope-hints', undefined, actor),
      api<BreakGlassSession[]>('/api/break-glass', undefined, actor),
    ])
    setProductRoles(pr)
    setObjectRoles(oroles)
    setTrustees(sug)
    setScopeHints(hints)
    setBgSessions(sessions)
  }, [actor])

  const buildAuditQuery = useCallback(() => {
    const q = new URLSearchParams()
    if (auditObject.trim()) q.set('objectKey', auditObject.trim())
    if (auditActor.trim()) q.set('actor', auditActor.trim())
    if (auditAction.trim()) q.set('action', auditAction.trim())
    if (auditFrom) q.set('from', new Date(auditFrom).toISOString())
    if (auditTo) q.set('to', new Date(auditTo).toISOString())
    q.set('take', '500')
    return q.toString()
  }, [auditObject, auditActor, auditAction, auditFrom, auditTo])

  const refreshAudit = useCallback(async () => {
    const items = await api<AuditEvent[]>(`/api/audit?${buildAuditQuery()}`, undefined, actor)
    setAuditEvents(items)
  }, [actor, buildAuditQuery])

  const refresh = useCallback(async () => {
    setError('')
    try {
      const [m, d, g, env, oob, c, smtpView] = await Promise.all([
        api<Me>('/api/me', undefined, actor),
        api<Domain[]>('/api/domains', undefined, actor),
        api<ControlledGpo[]>('/api/gpos', undefined, actor),
        api<AdEnvironment>('/api/ad/environment', undefined, actor),
        api<OobDetection[]>('/api/oob', undefined, actor),
        api<ControlledContainer[]>('/api/containers', undefined, actor),
        api<SmtpSettings>('/api/settings/smtp', undefined, actor),
      ])
      setMe(m)
      setDomains(d)
      setGpos(g)
      setAdEnv(env)
      setOobHits(oob)
      setContainers(c)
      setSmtp(smtpView)
      setSmtpHost(smtpView.host)
      setSmtpPort(smtpView.port)
      setSmtpFrom(smtpView.from)
      setSmtpUseSsl(smtpView.useSsl)
      setSmtpUser(smtpView.user)
      setSmtpRecipients(smtpView.alertRecipients.join(', '))
      if (selectedId) {
        const current = g.find((x) => x.id === selectedId) ?? null
        setSelected(current)
      }
      if (selectedContainerId) {
        setSelectedContainer(c.find((x) => x.id === selectedContainerId) ?? null)
      }
      await refreshDelegation()
      try {
        await refreshAudit()
      } catch {
        // Audit may be empty or briefly unavailable during bootstrap.
      }
      const fixture = await api<{ available: boolean; path?: string }>('/api/agpm/fixture', undefined, actor)
      if (fixture.available && fixture.path) {
        setAgpmPath((prev) => prev || fixture.path || '')
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }, [actor, selectedId, selectedContainerId, refreshDelegation, refreshAudit])

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
    setDiffResult(null)
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
      if (h.length >= 2) {
        setDiffFrom(h[h.length - 1]?.versionNumber ?? 1)
        setDiffTo(h[0]?.versionNumber ?? 1)
      } else if (h.length === 1) {
        setDiffFrom(h[0].versionNumber)
        setDiffTo(h[0].versionNumber)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function loadGpoDiff() {
    if (!selectedId) return
    setError('')
    try {
      const result = await api<VersionDiffResult>(
        `/api/gpos/${selectedId}/diff?from=${diffFrom}&to=${diffTo}`,
        undefined,
        actor,
      )
      setDiffResult(result)
      setMessage(`GPO Diff v${result.fromVersion} → v${result.toVersion}: ${result.changes.length} change(s).`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function takeControlContainer() {
    setError('')
    const domainId = domains[0]?.id
    if (!domainId) {
      setError('Register a domain first.')
      return
    }
    try {
      await api('/api/containers/take-control', {
        method: 'POST',
        body: JSON.stringify({
          domainId,
          kind: containerKind,
          containerKey,
          displayName: containerName,
        }),
      }, actor)
      const mode = adEnv ? adModeLabel(adEnv.effectiveMode) : 'DryRun'
      setMessage(`Container Take Control: ${containerName} (${mode} gpLink path).`)
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function selectContainer(id: string) {
    setSelectedContainerId(id)
    setContainerDiff(null)
    setError('')
    try {
      const [links, hist, list] = await Promise.all([
        api<ContainerLinksState>(`/api/containers/${id}/links`, undefined, actor),
        api<ContainerVersion[]>(`/api/containers/${id}/history`, undefined, actor),
        api<ControlledContainer[]>('/api/containers', undefined, actor),
      ])
      setContainerLinks(links)
      setContainerHistory(hist)
      setContainers(list)
      setSelectedContainer(list.find((c) => c.id === id) ?? null)
      if (hist.length >= 2) {
        setContainerDiffFrom(hist[hist.length - 1]?.versionNumber ?? 1)
        setContainerDiffTo(hist[0]?.versionNumber ?? 1)
      } else if (hist.length === 1) {
        setContainerDiffFrom(hist[0].versionNumber)
        setContainerDiffTo(hist[0].versionNumber)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function containerCheckout() {
    if (!selectedContainerId) return
    setError('')
    try {
      await api(`/api/containers/${selectedContainerId}/checkout`, { method: 'POST' }, actor)
      setMessage('Container checked out — Links-Editor bereit.')
      await refresh()
      await selectContainer(selectedContainerId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function saveContainerLinks() {
    if (!selectedContainerId || !containerLinks) return
    setError('')
    try {
      const links = await api<ContainerLinksState>(`/api/containers/${selectedContainerId}/links`, {
        method: 'PUT',
        body: JSON.stringify(containerLinks),
      }, actor)
      setContainerLinks(links)
      setMessage(`Links gespeichert (${links.links.length}).`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function containerCheckIn() {
    if (!selectedContainerId) return
    setError('')
    try {
      if (containerLinks) {
        await saveContainerLinks()
      }
      await api(`/api/containers/${selectedContainerId}/check-in`, {
        method: 'POST',
        body: JSON.stringify({ comment: containerCheckInComment || 'Container check-in from UI' }),
      }, actor)
      setContainerCheckInComment('')
      setMessage('Container Check-in → Waiting Approval.')
      await refresh()
      await selectContainer(selectedContainerId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function containerApprove() {
    if (!selectedContainerId) return
    setError('')
    try {
      await api(`/api/containers/${selectedContainerId}/approve`, {
        method: 'POST',
        body: JSON.stringify({ comment: containerReviewComment || null }),
      }, actor)
      setContainerReviewComment('')
      setMessage('Container Approved — bereit für Deploy.')
      await refresh()
      await selectContainer(selectedContainerId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function containerReject() {
    if (!selectedContainerId) return
    setError('')
    try {
      await api(`/api/containers/${selectedContainerId}/reject`, {
        method: 'POST',
        body: JSON.stringify({ comment: containerReviewComment || 'Rejected from UI' }),
      }, actor)
      setContainerReviewComment('')
      setMessage('Container Rejected — zurück Under Control.')
      await refresh()
      await selectContainer(selectedContainerId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function containerDeploy() {
    if (!selectedContainerId) return
    setError('')
    try {
      await api(`/api/containers/${selectedContainerId}/deploy`, { method: 'POST' }, actor)
      const mode = adEnv ? adModeLabel(adEnv.effectiveMode) : 'DryRun'
      setMessage(`Container Deployed — gpLink geschrieben (${mode}).`)
      await refresh()
      await selectContainer(selectedContainerId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function loadContainerDiff() {
    if (!selectedContainerId) return
    setError('')
    try {
      const result = await api<VersionDiffResult>(
        `/api/containers/${selectedContainerId}/diff?from=${containerDiffFrom}&to=${containerDiffTo}`,
        undefined,
        actor,
      )
      setContainerDiff(result)
      setMessage(`Container Diff v${result.fromVersion} → v${result.toVersion}: ${result.changes.length} change(s).`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  function moveLink(index: number, direction: -1 | 1) {
    setContainerLinks((prev) => {
      if (!prev) return prev
      const links = [...prev.links].sort((a, b) => a.order - b.order)
      const target = index + direction
      if (target < 0 || target >= links.length) return prev
      const tmp = links[index]
      links[index] = links[target]
      links[target] = tmp
      return {
        ...prev,
        links: links.map((l, i) => ({ ...l, order: i })),
      }
    })
  }

  function toggleLinkFlag(index: number, flag: 'enforced' | 'disabled') {
    setContainerLinks((prev) => {
      if (!prev) return prev
      const links = [...prev.links].sort((a, b) => a.order - b.order)
      links[index] = { ...links[index], [flag]: !links[index][flag] }
      return { ...prev, links }
    })
  }

  function removeLink(index: number) {
    setContainerLinks((prev) => {
      if (!prev) return prev
      const links = [...prev.links].sort((a, b) => a.order - b.order).filter((_, i) => i !== index)
      return { ...prev, links: links.map((l, i) => ({ ...l, order: i })) }
    })
  }

  function addLink() {
    setContainerLinks((prev) => {
      if (!prev) return prev
      if (prev.links.some((l) => l.gpoId.toLowerCase() === newLinkGpoId.toLowerCase())) {
        return prev
      }
      return {
        ...prev,
        links: [
          ...prev.links,
          {
            gpoId: newLinkGpoId,
            displayName: newLinkName,
            order: prev.links.length,
            enforced: false,
            disabled: false,
          },
        ],
      }
    })
  }

  async function saveSmtp() {
    setError('')
    try {
      const view = await api<SmtpSettings>('/api/settings/smtp', {
        method: 'PUT',
        body: JSON.stringify({
          host: smtpHost,
          port: smtpPort,
          from: smtpFrom,
          useSsl: smtpUseSsl,
          user: smtpUser,
          password: smtpPassword || null,
          alertRecipients: smtpRecipients.split(/[,;\s]+/).filter(Boolean),
        }),
      }, actor)
      setSmtp(view)
      setSmtpPassword('')
      setMessage(`SMTP gespeichert — Mode ${view.mode}. Auto-reset: aus.`)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function testSmtp() {
    setError('')
    try {
      const result = await api<{ ok: boolean; message: string; mode: string }>('/api/settings/smtp/test', {
        method: 'POST',
        body: JSON.stringify({ to: smtpTestTo || null }),
      }, actor)
      setMessage(result.message)
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

  async function activateBreakGlass() {
    setError('')
    try {
      const session = await api<BreakGlassSession>('/api/break-glass/activate', {
        method: 'POST',
        body: JSON.stringify({
          trusteePrincipal: bgTrustee,
          reason: bgReason,
          durationMinutes: bgMinutes,
        }),
      }, actor)
      setMessage(`Break Glass active until ${new Date(session.expiresAt).toLocaleString()} for ${session.trusteePrincipal}.`)
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function revokeBreakGlass(id: string) {
    setError('')
    try {
      await api(`/api/break-glass/${id}/revoke`, { method: 'POST' }, actor)
      setMessage('Break Glass session revoked.')
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function exportAuditCsv() {
    setError('')
    try {
      const headers: Record<string, string> = {}
      if (actor) headers['X-Gpd-Actor'] = actor
      const res = await fetch(`${API_BASE}/api/audit/export?${buildAuditQuery()}`, {
        credentials: 'include',
        headers,
      })
      if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        throw new Error((body as { error?: string }).error ?? `${res.status} ${res.statusText}`)
      }
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `gpd-audit-${new Date().toISOString().replace(/[:.]/g, '-')}.csv`
      a.click()
      URL.revokeObjectURL(url)
      setMessage('Audit CSV exported.')
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  const canEditTree = selected?.status === 2 && selected.checkedOutBy?.toLowerCase() === actor.toLowerCase()
  const canEditLinks =
    selectedContainer?.status === 2 &&
    selectedContainer.checkedOutBy?.toLowerCase() === actor.toLowerCase()
  const demoActors = me?.demoActors ?? [
    'CONTOSO\\gpd-admin',
    'CONTOSO\\gpd-editor',
    'CONTOSO\\gpd-approver',
    'CONTOSO\\gpd-deployer',
    'CONTOSO\\gpd-breakglass',
    'CONTOSO\\gpd-auditor',
  ]

  return (
    <div className="app-shell">
      <header className="hero">
        <p className="brand">Group Policy Director</p>
        <h1>Take Control → Checkout → Approve → Deploy</h1>
        <p className="lede">
          Slice 6 — Break Glass (zeitgebunden), Auditor (read-only), Audit-Log Filter/CSV. Branding bleibt Referenz-Tools. Kein Intune.
        </p>
        {me ? (
          <p className="identity">
            {me.identity} · {me.authMode}
            {me.isAuditor ? ' · Auditor' : ''}
            {me.breakGlassActive ? ' · Break Glass ACTIVE' : me.breakGlassEligible ? ' · Break Glass eligible' : ''}
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

        <section className="panel">
          <h2>Take Control — AD Container (gpLink)</h2>
          <div className="row">
            <label>
              Typ
              <select value={containerKind} onChange={(e) => setContainerKind(Number(e.target.value))}>
                <option value={1}>OU</option>
                <option value={2}>Domain</option>
                <option value={3}>Site</option>
              </select>
            </label>
            <label>
              Container-Key (DN)
              <input value={containerKey} onChange={(e) => setContainerKey(e.target.value)} />
            </label>
            <label>
              Anzeigename
              <input value={containerName} onChange={(e) => setContainerName(e.target.value)} />
            </label>
            <button type="button" onClick={() => void takeControlContainer()}>
              Take Control Container
            </button>
          </div>
          <p className="muted">
            Sites / Domain / OUs: gpLink Order, Enforce, Disable, Block Inheritance — gleicher Checkout → Approve → Deploy-Pfad.
          </p>
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
                value={delScopeKind}
                onChange={(e) => {
                  const kind = Number(e.target.value)
                  setDelScopeKind(kind)
                  const hint = scopeHints.find((h) => h.scopeKind === kind)
                  if (hint) setDelScopeKey(hint.scopeKey)
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
                value={bgTrustee}
                onChange={(e) => setBgTrustee(e.target.value)}
              />
            </label>
            <label>
              Dauer (Min.)
              <input
                type="number"
                min={1}
                max={480}
                value={bgMinutes}
                onChange={(e) => setBgMinutes(Number(e.target.value) || 60)}
              />
            </label>
            <label className="grow">
              Reason
              <input value={bgReason} onChange={(e) => setBgReason(e.target.value)} />
            </label>
            <button type="button" onClick={() => void activateBreakGlass()}>
              Activate
            </button>
          </div>
          <ul className="delegation-list">
            {bgSessions.map((s) => {
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
                    <button type="button" className="secondary" onClick={() => void revokeBreakGlass(s.id)}>
                      Revoke
                    </button>
                  ) : null}
                </li>
              )
            })}
          </ul>
        </section>

        <section className="panel audit-panel">
          <h2>Audit Log</h2>
          <p className="muted">
            Filter nach Objekt, Akteur, Aktion und Zeitraum. CSV-Export für Compliance. Auditor ist read-only.
          </p>
          <div className="row">
            <label>
              Object
              <input value={auditObject} onChange={(e) => setAuditObject(e.target.value)} placeholder="GPO GUID / DN" />
            </label>
            <label>
              Actor
              <input value={auditActor} onChange={(e) => setAuditActor(e.target.value)} placeholder="CONTOSO\…" />
            </label>
            <label>
              Action
              <input value={auditAction} onChange={(e) => setAuditAction(e.target.value)} placeholder="BreakGlass / Gpo." />
            </label>
            <label>
              From
              <input type="datetime-local" value={auditFrom} onChange={(e) => setAuditFrom(e.target.value)} />
            </label>
            <label>
              To
              <input type="datetime-local" value={auditTo} onChange={(e) => setAuditTo(e.target.value)} />
            </label>
            <button type="button" onClick={() => void refreshAudit().then(() => setMessage('Audit refreshed.')).catch((e) => setError(e instanceof Error ? e.message : String(e)))}>
              Filtern
            </button>
            <button type="button" className="secondary" onClick={() => void exportAuditCsv()}>
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
                {auditEvents.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="muted">Keine Events (Filter anpassen oder Workflow ausführen).</td>
                  </tr>
                ) : (
                  auditEvents.map((e) => (
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

        <section className="panel history-panel">
          <h2>Historie &amp; Diff (GPO)</h2>
          {history.length ? (
            <>
              <ol className="history">
                {history.map((v) => (
                  <li key={`${v.versionNumber}-${v.createdAt}`}>
                    <button
                      type="button"
                      className="history-pick"
                      onClick={() => {
                        setDiffFrom((prev) => (prev === v.versionNumber ? prev : Math.min(prev, v.versionNumber)))
                        setDiffTo(v.versionNumber)
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
                  <select value={diffFrom} onChange={(e) => setDiffFrom(Number(e.target.value))}>
                    {history.map((v) => (
                      <option key={`from-${v.versionNumber}`} value={v.versionNumber}>
                        v{v.versionNumber}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  To
                  <select value={diffTo} onChange={(e) => setDiffTo(Number(e.target.value))}>
                    {history.map((v) => (
                      <option key={`to-${v.versionNumber}`} value={v.versionNumber}>
                        v{v.versionNumber}
                      </option>
                    ))}
                  </select>
                </label>
                <button type="button" onClick={() => void loadGpoDiff()}>
                  Settings Diff
                </button>
              </div>
              {diffResult ? (
                <div className="diff-panel">
                  <p>
                    <strong>
                      {diffResult.objectType} v{diffResult.fromVersion} → v{diffResult.toVersion}
                    </strong>
                    {' · '}
                    {diffResult.changes.length} Änderung(en)
                  </p>
                  {diffResult.changes.length ? (
                    <ul className="diff-list">
                      {diffResult.changes.map((c) => (
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

        <section className="panel workflow-panel">
          <h2>Kontrollierte Container &amp; Links-Editor</h2>
          <ul className="gpo-list">
            {containers.map((c) => (
              <li key={c.id}>
                <button
                  type="button"
                  className={selectedContainerId === c.id ? 'selected' : ''}
                  onClick={() => void selectContainer(c.id)}
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
          {selectedContainerId ? (
            <>
              <div className="actions">
                <button type="button" onClick={() => void containerCheckout()}>
                  Checkout
                </button>
                <input
                  placeholder="Check-in Kommentar"
                  value={containerCheckInComment}
                  onChange={(e) => setContainerCheckInComment(e.target.value)}
                />
                <button type="button" onClick={() => void containerCheckIn()}>
                  Check-in
                </button>
              </div>
              <div className="actions">
                <input
                  placeholder="Approve/Reject Kommentar"
                  value={containerReviewComment}
                  onChange={(e) => setContainerReviewComment(e.target.value)}
                />
                <button type="button" onClick={() => void containerApprove()}>
                  Approve
                </button>
                <button type="button" className="secondary" onClick={() => void containerReject()}>
                  Reject
                </button>
                <button type="button" onClick={() => void containerDeploy()}>
                  Deploy
                </button>
              </div>
            </>
          ) : (
            <p className="muted">Container Take Control, dann auswählen.</p>
          )}

          {containerLinks ? (
            <div className="links-editor">
              <h3>gpLink Links</h3>
              <label className="checkbox-label">
                Block Inheritance
                <input
                  type="checkbox"
                  checked={containerLinks.blockInheritance}
                  disabled={!canEditLinks}
                  onChange={(e) =>
                    setContainerLinks((prev) =>
                      prev ? { ...prev, blockInheritance: e.target.checked } : prev,
                    )
                  }
                />
              </label>
              <ul className="links-list">
                {[...containerLinks.links]
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
                      {canEditLinks ? (
                        <div className="link-actions">
                          <button type="button" className="secondary" onClick={() => moveLink(index, -1)}>
                            ↑
                          </button>
                          <button type="button" className="secondary" onClick={() => moveLink(index, 1)}>
                            ↓
                          </button>
                          <button type="button" className="secondary" onClick={() => toggleLinkFlag(index, 'enforced')}>
                            Enforce
                          </button>
                          <button type="button" className="secondary" onClick={() => toggleLinkFlag(index, 'disabled')}>
                            Disable
                          </button>
                          <button type="button" className="secondary" onClick={() => removeLink(index)}>
                            Remove
                          </button>
                        </div>
                      ) : null}
                    </li>
                  ))}
              </ul>
              {canEditLinks ? (
                <>
                  <div className="row">
                    <label>
                      GPO GUID
                      <input value={newLinkGpoId} onChange={(e) => setNewLinkGpoId(e.target.value)} />
                    </label>
                    <label>
                      Anzeigename
                      <input value={newLinkName} onChange={(e) => setNewLinkName(e.target.value)} />
                    </label>
                    <button type="button" onClick={() => addLink()}>
                      Add Link
                    </button>
                    <button type="button" onClick={() => void saveContainerLinks()}>
                      Links speichern
                    </button>
                  </div>
                </>
              ) : (
                <p className="muted">Links editierbar nur bei Checkout durch denselben Akteur.</p>
              )}
            </div>
          ) : null}

          {containerHistory.length ? (
            <div className="container-history">
              <h3>Container Historie &amp; Diff</h3>
              <ol className="history">
                {containerHistory.map((v) => (
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
                    value={containerDiffFrom}
                    onChange={(e) => setContainerDiffFrom(Number(e.target.value))}
                  >
                    {containerHistory.map((v) => (
                      <option key={`cfrom-${v.versionNumber}`} value={v.versionNumber}>
                        v{v.versionNumber}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  To
                  <select
                    value={containerDiffTo}
                    onChange={(e) => setContainerDiffTo(Number(e.target.value))}
                  >
                    {containerHistory.map((v) => (
                      <option key={`cto-${v.versionNumber}`} value={v.versionNumber}>
                        v{v.versionNumber}
                      </option>
                    ))}
                  </select>
                </label>
                <button type="button" onClick={() => void loadContainerDiff()}>
                  Links Diff
                </button>
              </div>
              {containerDiff ? (
                <div className="diff-panel">
                  <p>
                    <strong>
                      Container v{containerDiff.fromVersion} → v{containerDiff.toVersion}
                    </strong>
                    {' · '}
                    {containerDiff.changes.length} Änderung(en)
                  </p>
                  <ul className="diff-list">
                    {containerDiff.changes.map((c) => (
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

        <section className="panel">
          <h2>SMTP (OOB Mail-first)</h2>
          <p className="muted">
            Runtime-Overlay für OOB-Alerts. Host leer = Log-only. Auto-Reset bleibt deaktiviert.
            {smtp ? ` Aktuell: ${smtp.mode}${smtp.passwordConfigured ? ' · Passwort gesetzt' : ''}.` : ''}
          </p>
          <div className="row">
            <label>
              Host
              <input value={smtpHost} onChange={(e) => setSmtpHost(e.target.value)} placeholder="leer = log-only" />
            </label>
            <label>
              Port
              <input
                type="number"
                value={smtpPort}
                onChange={(e) => setSmtpPort(Number(e.target.value) || 25)}
              />
            </label>
            <label>
              From
              <input value={smtpFrom} onChange={(e) => setSmtpFrom(e.target.value)} />
            </label>
            <label className="checkbox-label">
              SSL/TLS
              <input
                type="checkbox"
                checked={smtpUseSsl}
                onChange={(e) => setSmtpUseSsl(e.target.checked)}
              />
            </label>
          </div>
          <div className="row">
            <label>
              User
              <input value={smtpUser} onChange={(e) => setSmtpUser(e.target.value)} />
            </label>
            <label>
              Password {smtp?.passwordConfigured ? '(gesetzt — leer lassen behält)' : ''}
              <input
                type="password"
                value={smtpPassword}
                onChange={(e) => setSmtpPassword(e.target.value)}
                autoComplete="new-password"
              />
            </label>
            <label>
              Alert recipients
              <input value={smtpRecipients} onChange={(e) => setSmtpRecipients(e.target.value)} />
            </label>
          </div>
          <div className="actions">
            <button type="button" onClick={() => void saveSmtp()}>
              SMTP speichern
            </button>
            <input
              placeholder="Test-Empfänger (optional)"
              value={smtpTestTo}
              onChange={(e) => setSmtpTestTo(e.target.value)}
            />
            <button type="button" className="secondary" onClick={() => void testSmtp()}>
              Test senden
            </button>
          </div>
          <p className="muted">Auto-reset enabled: never (MVP mail-first).</p>
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
          API: {API_BASE || '/api (proxy)'} · Header {`X-Gpd-Actor`} · Slice 5 Container-Links / Diff / SMTP · Referenz-Tools branding · keine Tier-Gates · kein Auto-Reset
        </p>
      </footer>
    </div>
  )
}
