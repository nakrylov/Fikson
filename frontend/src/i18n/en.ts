import { ru } from './ru';

/**
 * English dictionary.
 *
 * For now we keep it 1:1 with `ru` keys to ensure type safety.
 * Replace values with proper translations later.
 */
export const en: typeof ru = {
  ...ru,
  tenant: {
    ...ru.tenant,
    switchLabel: 'Switch tenant',
    switchButton: 'Switch',
    switching: 'Switching…',
    switchSuccess: 'Tenant switched successfully.',
    switchForbidden: 'Forbidden',
    switchNotFound: 'Tenant not found'
  },
  members: {
    ...ru.members,
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
    ...ru.invites,
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
  contracts: {
    ...ru.contracts,
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
    counterparty: 'Counterparty',
    addCompany: '+ Add company',
    searchCompany: 'Search company',
    noResults: 'No results',
    createCompany: 'Create',
    selectOrCreateCompany: 'Select existing company or create a new one.',
    companyName: 'Company name'
  },
  rules: {
    ...ru.rules,
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
    ...ru.claims,
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
    ...ru.imports,
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
    ...ru.dashboard,
    title: 'Contract dashboard',
    totalPenalty: 'Total penalties',
    totalClaims: 'Total violations',
    shipments: 'Shipments affected',
    lastImport: 'Last import'
  },
  nav: {
    ...ru.nav,
    contracts: 'Contracts',
    claims: 'Claims',
    imports: 'Imports',
    members: 'Members',
    invites: 'Invites',
    logout: 'Logout'
  }
};

