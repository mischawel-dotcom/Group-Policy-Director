export type BreakGlassSession = {
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

export type AuditEvent = {
  id: string
  occurredAt: string
  actor: string
  action: string
  objectKey?: string
  outcome?: string
  detailJson?: string
}

export type Me = {
  identity: string
  authMode: string
  effectiveRoles: string[]
  membershipPrincipals?: string[]
  isAuditor?: boolean
  isProductAdministrator?: boolean
  canCreateGpo?: boolean
  breakGlassEligible?: boolean
  breakGlassActive?: boolean
  activeBreakGlassSession?: BreakGlassSession | null
  demoActors?: string[]
}

export type GeneralSettings = {
  gpoNamingRegex?: string | null
  namingEnforcementEnabled: boolean
  requireCheckInComment: boolean
  welcomeWizardStatus: number
  welcomeWizardCompletedAt?: string
  welcomeWizardCompletedBy?: string
}

export type WelcomeWizardStep = {
  id: string
  title: string
  description: string
  done: boolean
  optional: boolean
}

export type WelcomeWizard = {
  status: number
  isDismissed: boolean
  canManage: boolean
  steps: WelcomeWizardStep[]
  completedAt?: string
  completedBy?: string
}

export type RestrictedPolicyArea = {
  id: string
  areaPath: string
  label?: string
  allowedTrustees: string[]
  createdBy: string
  createdAt: string
}

export type Domain = {
  id: string
  dnsName: string
  netBiosName: string
}

export type ControlledGpo = {
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

export type GpoVersion = {
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

export type ControlledContainer = {
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

export type GpoLinkEntry = {
  gpoId: string
  displayName?: string
  order: number
  enforced: boolean
  disabled: boolean
}

export type ContainerLinksState = {
  blockInheritance: boolean
  links: GpoLinkEntry[]
}

export type ContainerVersion = {
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

export type DiffChange = {
  path: string
  kind: string
  before?: string
  after?: string
}

export type VersionDiffResult = {
  fromVersion: number
  toVersion: number
  objectKey: string
  objectType: string
  changes: DiffChange[]
}

export type SmtpSettings = {
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

export type PolicyTreeNode = {
  path: string
  name: string
  value?: string
  state?: string
  isEditable?: boolean
  isStub?: boolean
  settingType?: string
  children: PolicyTreeNode[]
}

export type AdEnvironment = {
  isDomainJoined: boolean
  computerDnsDomain?: string
  computerNetBiosDomain?: string
  directoryReachable: boolean
  gpmcAvailable: boolean
  forceDryRunConfigured: boolean
  effectiveMode: number
  modeReason: string
}

export type OobDetection = {
  id: string
  controlledGpoId: string
  detectedAt: string
  productionHash: string
  lastControlledHash: string
  diffSummary?: string
  emailSent: boolean
  resolution?: string
}

export type ProductRoleAssignment = {
  id: string
  role: number
  trusteePrincipal: string
  grantedBy: string
  grantedAt: string
}

export type DelegationGrant = {
  id: string
  scopeKind: number
  scopeKey: string
  role: number
  trusteePrincipal: string
  inheritToChildren: boolean
  grantedBy: string
  grantedAt: string
}

export type SuggestedTrustee = {
  principal: string
  kind: string
  description?: string
}

export type ScopeHint = {
  scopeKind: number
  scopeKey: string
  label?: string
}

export type AgpmEntry = {
  sourceGpoId: string
  displayName?: string
  versionCount: number
  versionLabels: string[]
  mappedControlledGpoId?: string
  mappingStatus: string
  notes?: string
}

export type AgpmConflict = {
  gpoId: string
  reason: string
  severity?: string
}

export type AgpmReport = {
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

export type ScheduledDeploy = {
  id: string
  objectKind: number
  objectId: string
  objectKey: string
  displayName: string
  pendingVersionId: string
  scheduledFor: string
  scheduledBy: string
  createdAt: string
  rollbackOnFailure: boolean
  simulateFailure: boolean
  status: number
  startedAt?: string
  completedAt?: string
  errorMessage?: string
  cancelledAt?: string
  cancelledBy?: string
}
