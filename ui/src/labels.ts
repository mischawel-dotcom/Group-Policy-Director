export function statusLabel(status: number): string {
  switch (status) {
    case 1: return 'Under Control'
    case 2: return 'Checked Out'
    case 3: return 'Waiting Approval'
    case 4: return 'Deployed'
    case 5: return 'Approved'
    case 6: return 'Scheduled'
    default: return String(status)
  }
}

export function scheduleStatusLabel(status: number): string {
  switch (status) {
    case 1: return 'Pending'
    case 2: return 'Running'
    case 3: return 'Succeeded'
    case 4: return 'Failed'
    case 5: return 'RolledBack'
    case 6: return 'Cancelled'
    default: return String(status)
  }
}

export function scheduleObjectKindLabel(kind: number): string {
  return kind === 2 ? 'Container' : 'GPO'
}

export function adModeLabel(mode: number): string {
  return mode === 1 ? 'Live' : 'DryRun'
}

export function productRoleLabel(role: number): string {
  switch (role) {
    case 1: return 'ProductAdministrator'
    case 2: return 'GpoCreator'
    case 3: return 'BreakGlass'
    case 4: return 'Auditor'
    default: return String(role)
  }
}

export function objectRoleLabel(role: number): string {
  switch (role) {
    case 1: return 'Editor'
    case 2: return 'Approver'
    case 3: return 'Deployer'
    default: return String(role)
  }
}

export function scopeKindLabel(kind: number): string {
  switch (kind) {
    case 1: return 'OU'
    case 2: return 'GPO'
    case 3: return 'Container'
    default: return String(kind)
  }
}

export function containerKindLabel(kind: number): string {
  switch (kind) {
    case 1: return 'OU'
    case 2: return 'Domain'
    case 3: return 'Site'
    default: return String(kind)
  }
}
