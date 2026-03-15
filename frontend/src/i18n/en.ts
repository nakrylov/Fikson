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
  }
};

