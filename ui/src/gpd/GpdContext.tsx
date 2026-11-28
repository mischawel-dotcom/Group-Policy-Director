import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, API_BASE } from '../api'
import { applyLocalPatch } from '../components/TreeNodeView'
import {
  adModeLabel,
  objectRoleLabel,
  productRoleLabel,
  scopeKindLabel,
} from '../labels'
import type {
  AdEnvironment,
  AgpmReport,
  AuditEvent,
  BreakGlassSession,
  ContainerLinksState,
  ContainerVersion,
  ControlledContainer,
  ControlledGpo,
  DelegationGrant,
  Domain,
  GeneralSettings,
  GpoVersion,
  Me,
  OobDetection,
  PolicyTreeNode,
  ProductRoleAssignment,
  RestrictedPolicyArea,
  ScheduledDeploy,
  ScopeHint,
  SmtpSettings,
  SuggestedTrustee,
  VersionDiffResult,
  WelcomeWizard,
} from '../types'

export function useGpdApp() {
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
  const [general, setGeneral] = useState<GeneralSettings | null>(null)
  const [namingRegex, setNamingRegex] = useState('')
  const [createName, setCreateName] = useState('Policy_Workstations-Baseline-01')
  const [createCopyFrom, setCreateCopyFrom] = useState('')
  const [createCheckout, setCreateCheckout] = useState(false)
  const [renameName, setRenameName] = useState('')
  const [restricted, setRestricted] = useState<RestrictedPolicyArea[]>([])
  const [areaHints, setAreaHints] = useState<string[]>([])
  const [rpAreaPath, setRpAreaPath] = useState('/Computer/Policies/Windows Settings/Security Settings')
  const [rpTrustees, setRpTrustees] = useState('CONTOSO\\GPD-Editors')
  const [rpLabel, setRpLabel] = useState('Security Settings')
  const [wizard, setWizard] = useState<WelcomeWizard | null>(null)
  const [showWizard, setShowWizard] = useState(false)
  const [deployQueue, setDeployQueue] = useState<ScheduledDeploy[]>([])
  const [scheduleAt, setScheduleAt] = useState(() => {
    const d = new Date(Date.now() + 60_000)
    d.setSeconds(0, 0)
    return d.toISOString().slice(0, 16)
  })
  const [rollbackOnFail, setRollbackOnFail] = useState(true)
  const [simulateFail, setSimulateFail] = useState(false)
  const [containerScheduleAt, setContainerScheduleAt] = useState(() => {
    const d = new Date(Date.now() + 60_000)
    d.setSeconds(0, 0)
    return d.toISOString().slice(0, 16)
  })
  const [containerRollbackOnFail, setContainerRollbackOnFail] = useState(true)
  const [containerSimulateFail, setContainerSimulateFail] = useState(false)

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
      const [m, d, g, env, oob, c, smtpView, gen, rp, hints, wiz, queue] = await Promise.all([
        api<Me>('/api/me', undefined, actor),
        api<Domain[]>('/api/domains', undefined, actor),
        api<ControlledGpo[]>('/api/gpos', undefined, actor),
        api<AdEnvironment>('/api/ad/environment', undefined, actor),
        api<OobDetection[]>('/api/oob', undefined, actor),
        api<ControlledContainer[]>('/api/containers', undefined, actor),
        api<SmtpSettings>('/api/settings/smtp', undefined, actor),
        api<GeneralSettings>('/api/settings/general', undefined, actor),
        api<RestrictedPolicyArea[]>('/api/restricted-policies', undefined, actor),
        api<string[]>('/api/restricted-policies/area-hints', undefined, actor),
        api<WelcomeWizard>('/api/wizard', undefined, actor),
        api<ScheduledDeploy[]>('/api/deploy-queue', undefined, actor),
      ])
      setMe(m)
      setDomains(d)
      setGpos(g)
      setAdEnv(env)
      setOobHits(oob)
      setContainers(c)
      setDeployQueue(queue)
      setSmtp(smtpView)
      setSmtpHost(smtpView.host)
      setSmtpPort(smtpView.port)
      setSmtpFrom(smtpView.from)
      setSmtpUseSsl(smtpView.useSsl)
      setSmtpUser(smtpView.user)
      setSmtpRecipients(smtpView.alertRecipients.join(', '))
      setGeneral(gen)
      setNamingRegex(gen.gpoNamingRegex ?? '')
      setRestricted(rp)
      setAreaHints(hints)
      setWizard(wiz)
      setShowWizard(Boolean(wiz.canManage && !wiz.isDismissed))
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

  async function createGpo() {
    setError('')
    const domainId = domains[0]?.id
    if (!domainId) {
      setError('Register a domain first.')
      return
    }
    try {
      const gpo = await api<ControlledGpo>('/api/gpos', {
        method: 'POST',
        body: JSON.stringify({
          domainId,
          displayName: createName,
          copyFromControlledGpoId: createCopyFrom || null,
          checkoutAfterCreate: createCheckout,
        }),
      }, actor)
      setMessage(`GPO erstellt: ${gpo.displayName}${createCheckout ? ' (checked out)' : ''}.`)
      await refresh()
      await selectGpo(gpo.id)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function renameGpo() {
    setError('')
    if (!selectedId) {
      setError('Select a GPO first.')
      return
    }
    try {
      const gpo = await api<ControlledGpo>(`/api/gpos/${selectedId}/rename`, {
        method: 'POST',
        body: JSON.stringify({ displayName: renameName }),
      }, actor)
      setMessage(`Umbenannt: ${gpo.displayName}`)
      await refresh()
      await selectGpo(gpo.id)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function saveNaming() {
    setError('')
    try {
      const view = await api<GeneralSettings>('/api/settings/general', {
        method: 'PUT',
        body: JSON.stringify(
          namingRegex.trim()
            ? { gpoNamingRegex: namingRegex.trim() }
            : { clearGpoNamingRegex: true },
        ),
      }, actor)
      setGeneral(view)
      setNamingRegex(view.gpoNamingRegex ?? '')
      setMessage(
        view.namingEnforcementEnabled
          ? `Naming regex aktiv: ${view.gpoNamingRegex}`
          : 'Naming-Enforcement deaktiviert.',
      )
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function upsertRestricted() {
    setError('')
    try {
      await api('/api/restricted-policies', {
        method: 'POST',
        body: JSON.stringify({
          areaPath: rpAreaPath,
          label: rpLabel || null,
          allowedTrustees: rpTrustees.split(/[,;\s]+/).filter(Boolean),
        }),
      }, actor)
      setMessage(`Restricted Policy gesetzt: ${rpAreaPath}`)
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function deleteRestricted(id: string) {
    setError('')
    try {
      await api(`/api/restricted-policies/${id}`, { method: 'DELETE' }, actor)
      setMessage('Restricted Policy entfernt.')
      await refresh()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function completeWizard() {
    setError('')
    try {
      const wiz = await api<WelcomeWizard>('/api/wizard/complete', { method: 'POST' }, actor)
      setWizard(wiz)
      setShowWizard(false)
      setMessage('Welcome Wizard abgeschlossen.')
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function skipWizard() {
    setError('')
    try {
      const wiz = await api<WelcomeWizard>('/api/wizard/skip', { method: 'POST' }, actor)
      setWizard(wiz)
      setShowWizard(false)
      setMessage('Welcome Wizard übersprungen.')
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
      const current = list.find((g) => g.id === id) ?? null
      setSelected(current)
      if (current) setRenameName(current.displayName)
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
      await api(
        `/api/containers/${selectedContainerId}/deploy`,
        {
          method: 'POST',
          body: JSON.stringify({
            rollbackOnFailure: containerRollbackOnFail,
            simulateFailure: containerSimulateFail,
          }),
        },
        actor,
      )
      const mode = adEnv ? adModeLabel(adEnv.effectiveMode) : 'DryRun'
      setMessage(`Container Deployed — gpLink geschrieben (${mode}).`)
      await refresh()
      await selectContainer(selectedContainerId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function scheduleContainerDeploy() {
    if (!selectedContainerId) return
    setError('')
    try {
      const scheduledFor = new Date(containerScheduleAt).toISOString()
      const job = await api<ScheduledDeploy>(
        `/api/containers/${selectedContainerId}/schedule-deploy`,
        {
          method: 'POST',
          body: JSON.stringify({
            scheduledFor,
            rollbackOnFailure: containerRollbackOnFail,
            simulateFailure: containerSimulateFail,
          }),
        },
        actor,
      )
      setMessage(
        `Container Deploy geplant für ${new Date(job.scheduledFor).toLocaleString('de-DE')} (Rollback-on-fail: ${job.rollbackOnFailure ? 'ja' : 'nein'}).`,
      )
      await refresh()
      await selectContainer(selectedContainerId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function cancelContainerSchedule() {
    if (!selectedContainerId) return
    setError('')
    try {
      await api(`/api/containers/${selectedContainerId}/cancel-schedule`, { method: 'POST' }, actor)
      setMessage('Container-Schedule storniert — Status wieder Approved.')
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
      await api(
        `/api/gpos/${selectedId}/deploy`,
        {
          method: 'POST',
          body: JSON.stringify({
            rollbackOnFailure: rollbackOnFail,
            simulateFailure: simulateFail,
          }),
        },
        actor,
      )
      const mode = adEnv ? adModeLabel(adEnv.effectiveMode) : 'DryRun'
      setMessage(`Deployed — Produktion aktualisiert (${mode}).`)
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function scheduleDeploy() {
    if (!selectedId) return
    setError('')
    try {
      const scheduledFor = new Date(scheduleAt).toISOString()
      const job = await api<ScheduledDeploy>(
        `/api/gpos/${selectedId}/schedule-deploy`,
        {
          method: 'POST',
          body: JSON.stringify({
            scheduledFor,
            rollbackOnFailure: rollbackOnFail,
            simulateFailure: simulateFail,
          }),
        },
        actor,
      )
      setMessage(
        `GPO Deploy geplant für ${new Date(job.scheduledFor).toLocaleString('de-DE')} (Rollback-on-fail: ${job.rollbackOnFailure ? 'ja' : 'nein'}).`,
      )
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function cancelGpoSchedule() {
    if (!selectedId) return
    setError('')
    try {
      await api(`/api/gpos/${selectedId}/cancel-schedule`, { method: 'POST' }, actor)
      setMessage('GPO-Schedule storniert — Status wieder Approved.')
      await refresh()
      await selectGpo(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function processDueDeploys() {
    setError('')
    try {
      const done = await api<ScheduledDeploy[]>('/api/deploy-queue/process-due', { method: 'POST' }, actor)
      setMessage(`Process-due: ${done.length} Job(s) verarbeitet.`)
      await refresh()
      if (selectedId) await selectGpo(selectedId)
      if (selectedContainerId) await selectContainer(selectedContainerId)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function cancelQueueJob(id: string) {
    setError('')
    try {
      await api(`/api/deploy-queue/${id}/cancel`, { method: 'POST' }, actor)
      setMessage('Schedule storniert.')
      await refresh()
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

  return {
    API_BASE,
    me, actor, setActor, demoActors,
    domains, gpos, selectedId, selected, tree, history,
    diffFrom, setDiffFrom, diffTo, setDiffTo, diffResult,
    containers, selectedContainerId, selectedContainer, containerLinks, setContainerLinks,
    containerHistory, containerDiffFrom, setContainerDiffFrom, containerDiffTo, setContainerDiffTo, containerDiff,
    containerKind, setContainerKind, containerKey, setContainerKey, containerName, setContainerName,
    containerCheckInComment, setContainerCheckInComment, containerReviewComment, setContainerReviewComment,
    newLinkGpoId, setNewLinkGpoId, newLinkName, setNewLinkName,
    smtp, smtpHost, setSmtpHost, smtpPort, setSmtpPort, smtpFrom, setSmtpFrom, smtpUseSsl, setSmtpUseSsl,
    smtpUser, setSmtpUser, smtpPassword, setSmtpPassword, smtpRecipients, setSmtpRecipients, smtpTestTo, setSmtpTestTo,
    adEnv, oobHits,
    dnsName, setDnsName, netBios, setNetBios, gpoId, setGpoId, gpoName, setGpoName,
    checkInComment, setCheckInComment, reviewComment, setReviewComment,
    agpmPath, setAgpmPath, agpmReport, agpmFile, setAgpmFile,
    productRoles, objectRoles, trustees, scopeHints,
    delScopeKind, setDelScopeKind, delScopeKey, setDelScopeKey, delRole, setDelRole, delTrustee, setDelTrustee, delInherit, setDelInherit,
    prodRole, setProdRole, prodTrustee, setProdTrustee,
    bgSessions, bgTrustee, setBgTrustee, bgReason, setBgReason, bgMinutes, setBgMinutes,
    auditEvents, auditObject, setAuditObject, auditActor, setAuditActor, auditAction, setAuditAction,
    auditFrom, setAuditFrom, auditTo, setAuditTo,
    message, error, setMessage, setError, pendingEdits,
    general, namingRegex, setNamingRegex, createName, setCreateName, createCopyFrom, setCreateCopyFrom,
    createCheckout, setCreateCheckout, renameName, setRenameName,
    restricted, areaHints, rpAreaPath, setRpAreaPath, rpTrustees, setRpTrustees, rpLabel, setRpLabel,
    wizard, showWizard, setShowWizard,
    deployQueue, scheduleAt, setScheduleAt, rollbackOnFail, setRollbackOnFail, simulateFail, setSimulateFail,
    containerScheduleAt, setContainerScheduleAt, containerRollbackOnFail, setContainerRollbackOnFail,
    containerSimulateFail, setContainerSimulateFail,
    canEditTree, canEditLinks,
    refresh, refreshAudit, refreshDelegation,
    registerDomain, takeControl, createGpo, renameGpo, saveNaming,
    upsertRestricted, deleteRestricted, completeWizard, skipWizard,
    selectGpo, loadGpoDiff, takeControlContainer, selectContainer,
    containerCheckout, saveContainerLinks, containerCheckIn, containerApprove, containerReject, containerDeploy,
    scheduleContainerDeploy, cancelContainerSchedule, loadContainerDiff,
    moveLink, toggleLinkFlag, removeLink, addLink,
    saveSmtp, testSmtp, checkout, onTreeChange, saveSettings, checkIn, approve, reject, deploy,
    scheduleDeploy, cancelGpoSchedule, processDueDeploys, cancelQueueJob,
    runOobScan, simulateDrift, agpmDryRunPath, agpmDryRunUpload,
    grantObjectRole, revokeObjectRole, grantProductRole, revokeProductRole,
    activateBreakGlass, revokeBreakGlass, exportAuditCsv,
  }
}

export type GpdContextValue = ReturnType<typeof useGpdApp>

const GpdContext = createContext<GpdContextValue | null>(null)

export function GpdProvider({ children }: { children: ReactNode }) {
  const value = useGpdApp()
  return <GpdContext.Provider value={value}>{children}</GpdContext.Provider>
}

export function useGpd(): GpdContextValue {
  const ctx = useContext(GpdContext)
  if (!ctx) throw new Error('useGpd must be used within GpdProvider')
  return ctx
}
