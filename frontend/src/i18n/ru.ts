/**
 * Russian dictionary (default).
 *
 * NOTE:
 * For now it contains the current UI strings (some may still be English).
 * We keep `ru` as the default locale to avoid runtime switching complexity.
 */
export const ru = {
  common: {
    loading: 'Loading…',
    working: 'Working…',
    joining: 'Joining…',
    creating: 'Creating…',
    refresh: 'Refresh',
    logout: 'Logout',
    cancel: 'Cancel',
    submit: 'Submit',
    close: 'Close',
    backToList: 'Back to list',
    noData: 'No data',
    unknown: '(unknown)',
    copied: 'Copied',
    copyFailed: 'Copy failed',
    requestFailed: 'Request failed',
    serverError: 'Server error',
    forbidden: 'Forbidden',
    unauthorized: 'Unauthorized',
    validationError: 'Validation error'
  },
  auth: {
    loginTitle: 'Fixon Login',
    email: 'Email',
    password: 'Password',
    signIn: 'Sign in',
    signingIn: 'Signing in…',
    invalidCredentials: 'Invalid credentials',
    authenticated: 'Authenticated',
    tenantIdLabel: 'tenantId',
    roleLabel: 'role',
    goToContracts: 'Go to contracts'
  },
  tenant: {
    welcomeTitle: 'Welcome to Fixon',
    noCompanyYet: 'You are not part of any company yet.',
    companyName: 'Company name',
    companyNamePlaceholder: 'Company Name',
    createCompany: 'Create Company',
    creatingCompany: 'Creating…',
    switchLabel: 'Switch tenant',
    switchButton: 'Switch',
    switching: 'Switching…',
    switchSuccess: 'Tenant switched successfully.',
    switchForbidden: 'Forbidden',
    switchNotFound: 'Tenant not found',
    nameTooShort: 'Company name must be at least 2 characters.',
    alreadyInCompany: 'You already belong to a company.',
    goToContracts: 'Go to contracts',
    pleaseLoginFirst: 'Please log in first.'
  },
  contracts: {
    title: 'Contracts',
    detailsTitle: 'Contract details',
    tenantIdFromJwt: 'tenantId (from JWT)',
    roleFromJwt: 'role (from JWT)',
    createContract: 'Create Contract',
    name: 'Name',
    edit: 'Edit',
    save: 'Save',
    cancel: 'Cancel',
    conflictError: 'This contract was modified by another user. Please reload.',
    versions: 'Versions',
    createVersion: 'Create version',
    signVersion: 'Sign version',
    activateVersion: 'Activate version',
    versionNumber: 'Version',
    status: 'Status',
    createdAt: 'Created at',
    counterpartyId: 'CounterpartyId',
    guidPlaceholder: 'GUID',
    actions: 'actions',
    view: 'View',
    table: {
      id: 'id',
      name: 'name',
      counterpartyId: 'counterpartyId',
      status: 'status',
      actions: 'actions'
    }
  },
  invite: {
    inviteUserTitle: 'Invite User',
    email: 'Email',
    role: 'Role',
    createInvite: 'Create Invite',
    inviteTokenLabel: 'inviteToken',
    expiresAtLabel: 'expiresAt',
    joinLinkLabel: 'join link',
    copyInviteLink: 'Copy Invite Link',
    roleMember: 'Member',
    roleViewer: 'Viewer',
    joinTenantTitle: 'Join tenant',
    invitedToJoinPrefix: 'You were invited to join',
    registerToAccept: 'Register to accept invite',
    registerAndJoin: 'Register & Join',
    acceptInvite: 'Accept Invite',
    joinHeadingPrefix: 'Join',
    missingTenantId: 'Missing tenantId',
    alreadyMember: 'User is already a member',
    warningDifferentEmail: 'Warning: this invite was issued for a different email address.',
    differentEmailError: 'This invite was issued for a different email address.',
    invalidInvite: 'Invalid invite',
    expiredInvite: 'Expired invite',
    inviteAlreadyAccepted: 'Invite already accepted'
  },
  members: {
    title: 'Tenant members',
    email: 'Email',
    role: 'Role',
    status: 'Status',
    createdAt: 'Created at',
    empty: 'No members',
    forbidden: 'Forbidden',
    openPage: 'Open members page'
  },
  invites: {
    title: 'Tenant invites',
    email: 'Email',
    role: 'Role',
    status: 'Status',
    expiresAt: 'Expires at',
    actions: 'Actions',
    copyLink: 'Copy link',
    revoke: 'Revoke',
    empty: 'No invites',
    forbidden: 'Forbidden',
    openPage: 'Open invites page',
    copySuccess: 'Invite link copied',
    revokeSuccess: 'Invite revoked',
    revokeError: 'Failed to revoke invite'
  },
  rules: {
    title: 'SLA rules',
    metric: 'Metric',
    operator: 'Operator',
    threshold: 'Threshold',
    penalty: 'Penalty amount',
    create: 'Create rule',
    delete: 'Delete',
    empty: 'No rules'
  },
  claims: {
    title: 'Claims',
    summaryTitle: 'Summary',
    totalClaims: 'Total claims',
    totalPenalty: 'Total penalties',
    shipment: 'Shipment',
    rule: 'Rule',
    penalty: 'Penalty',
    created: 'Created at',
    empty: 'No claims',
    openPage: 'Open claims page'
  },
  imports: {
    title: 'Fact imports',
    openPage: 'Open imports page',
    selectFile: 'CSV file',
    upload: 'Upload',
    uploading: 'Uploading…',
    historyTitle: 'Import history',
    file: 'File',
    rows: 'Rows imported',
    claims: 'Claims generated',
    date: 'Created at',
    empty: 'No imports yet'
  },
  dashboard: {
    title: 'Contract dashboard',
    totalPenalty: 'Total penalties',
    totalClaims: 'Total violations',
    shipments: 'Shipments affected',
    lastImport: 'Last import'
  },
  errors: {
    failedToLoadContracts: 'Request failed',
    failedToLoadContract: 'Failed to load contract.'
  }
} as const;

