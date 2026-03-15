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
  errors: {
    failedToLoadContracts: 'Request failed',
    failedToLoadContract: 'Failed to load contract.'
  }
} as const;

