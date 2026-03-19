import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ApiError,
  apiRequest,
  Counterparty,
  createCounterparty,
  createInvite,
  CreateInviteResponse,
  getCounterparties,
  switchTenant
} from '../api/api';
import { useAuth } from '../hooks/useAuth';
import { t } from '../i18n';

/**
 * /contracts
 * Contracts list + minimal create form (no UI libs).
 */

export interface Contract {
  id: string;
  name: string;
  counterpartyId: string;
  status?: string;
};

type ContractsListResponse = {
  tenantId: string;
  items: Array<{
    id: string;
    name: string;
    counterpartyId: string;
    status?: string | number;
  }>;
};

export function ContractsPage() {
  const { tenantId, role, memberships, setToken, logout } = useAuth();
  const navigate = useNavigate();
  const counterpartyDropdownRef = React.useRef<HTMLDivElement | null>(null);

  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [createName, setCreateName] = useState<string>('');
  const [createCounterpartyId, setCreateCounterpartyId] = useState<string>('');
  const [createLoading, setCreateLoading] = useState<boolean>(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [counterparties, setCounterparties] = useState<Counterparty[]>([]);
  const [loadingCounterparties, setLoadingCounterparties] = useState<boolean>(false);
  const [counterpartyError, setCounterpartyError] = useState<string | null>(null);
  const [counterpartyQuery, setCounterpartyQuery] = useState<string>('');
  const [filteredCounterparties, setFilteredCounterparties] = useState<Counterparty[]>([]);
  const [isDropdownOpen, setIsDropdownOpen] = useState<boolean>(false);
  const [isCreatingInline, setIsCreatingInline] = useState<boolean>(false);

  const [inviteEmail, setInviteEmail] = useState<string>('');
  // Backend expects numeric enum ids: Admin=0, Member=1, Viewer=2.
  const [inviteRole, setInviteRole] = useState<'1' | '2'>('1');
  const [inviteLoading, setInviteLoading] = useState<boolean>(false);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [inviteResult, setInviteResult] = useState<CreateInviteResponse | null>(null);
  const [copyStatus, setCopyStatus] = useState<string | null>(null);
  const [selectedTenantId, setSelectedTenantId] = useState<string>('');
  const [switchLoading, setSwitchLoading] = useState<boolean>(false);
  const [switchError, setSwitchError] = useState<string | null>(null);
  const [switchSuccess, setSwitchSuccess] = useState<string | null>(null);

  const mapToContract = useCallback((x: ContractsListResponse['items'][number]): Contract => {
    return {
      id: x.id,
      name: x.name,
      counterpartyId: x.counterpartyId,
      status: x.status !== undefined && x.status !== null ? String(x.status) : undefined
    };
  }, []);

  const loadContracts = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const data = await apiRequest<ContractsListResponse>('/api/contracts');
      setContracts(data.items.map(mapToContract));
    } catch (e: unknown) {
      if (e instanceof ApiError) {
        if (e.status === 403) setLoadError(t.common.forbidden);
        else if (e.status >= 500) setLoadError(t.common.serverError);
        else setLoadError(t.common.requestFailed);
      } else {
        setLoadError(t.common.requestFailed);
      }
    } finally {
      setLoading(false);
    }
  }, [mapToContract]);

  useEffect(() => {
    void loadContracts();
  }, [loadContracts]);

  const loadCounterparties = useCallback(async () => {
    setLoadingCounterparties(true);
    setCounterpartyError(null);
    try {
      const items = await getCounterparties();
      setCounterparties(items);
      setFilteredCounterparties(items);
      if (!createCounterpartyId && items.length > 0) {
        setCreateCounterpartyId(items[0].id);
      }
    } catch (e: unknown) {
      if (e instanceof ApiError) {
        if (e.status === 403) setCounterpartyError(t.common.forbidden);
        else if (e.status >= 500) setCounterpartyError(t.common.serverError);
        else setCounterpartyError(t.common.requestFailed);
      } else {
        setCounterpartyError(t.common.requestFailed);
      }
    } finally {
      setLoadingCounterparties(false);
    }
  }, [createCounterpartyId]);

  useEffect(() => {
    void loadCounterparties();
  }, [loadCounterparties]);

  useEffect(() => {
    setSelectedTenantId(tenantId ?? '');
  }, [tenantId]);

  useEffect(() => {
    const query = counterpartyQuery.trim().toLowerCase();
    if (!query) {
      setFilteredCounterparties(counterparties);
      return;
    }

    setFilteredCounterparties(
      counterparties.filter((cp) => cp.name.toLowerCase().includes(query))
    );
  }, [counterpartyQuery, counterparties]);

  useEffect(() => {
    const onDocumentMouseDown = (event: MouseEvent) => {
      if (!counterpartyDropdownRef.current) return;
      if (!counterpartyDropdownRef.current.contains(event.target as Node)) {
        setIsDropdownOpen(false);
      }
    };

    document.addEventListener('mousedown', onDocumentMouseDown);
    return () => {
      document.removeEventListener('mousedown', onDocumentMouseDown);
    };
  }, []);

  const onCreateSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setCreateError(null);
      if (!createCounterpartyId) {
        setCreateError(t.common.validationError);
        return;
      }
      setCreateLoading(true);
      try {
        const idempotencyKey = crypto.randomUUID();

        await apiRequest('/api/contracts', {
          method: 'POST',
          headers: {
            'Idempotency-Key': idempotencyKey
          },
          body: {
            name: createName,
            counterpartyId: createCounterpartyId
          }
        });

        // Refresh list and reset form on success.
        await loadContracts();
        setCreateName('');
        setCreateCounterpartyId('');
        setCounterpartyQuery('');
        setShowCreate(false);
      } catch (e2: unknown) {
        if (e2 instanceof ApiError) {
          if (e2.status === 400) setCreateError(t.common.validationError);
          else if (e2.status === 403) setCreateError(t.common.forbidden);
          else if (e2.status >= 500) setCreateError(t.common.serverError);
          else setCreateError(t.common.requestFailed);
        } else {
          setCreateError(t.common.requestFailed);
        }
      } finally {
        setCreateLoading(false);
      }
    },
    [createName, createCounterpartyId, loadContracts]
  );

  const onSelectCounterparty = useCallback((counterparty: Counterparty) => {
    setCreateCounterpartyId(counterparty.id);
    setCounterpartyQuery(counterparty.name);
    setIsDropdownOpen(false);
  }, []);

  const onCreateCounterpartyInline = useCallback(async () => {
    const name = counterpartyQuery.trim();
    if (name.length < 2) {
      setCounterpartyError(t.common.validationError);
      return;
    }

    setCounterpartyError(null);
    setIsCreatingInline(true);
    try {
      const created = await createCounterparty(name);
      await loadCounterparties();
      setCreateCounterpartyId(created.id);
      setCounterpartyQuery('');
      setIsDropdownOpen(false);
    } catch (e: unknown) {
      if (e instanceof ApiError) {
        if (e.status === 400) setCounterpartyError(t.common.validationError);
        else if (e.status === 403) setCounterpartyError(t.common.forbidden);
        else if (e.status >= 500) setCounterpartyError(t.common.serverError);
        else setCounterpartyError(t.common.requestFailed);
      } else {
        setCounterpartyError(t.common.requestFailed);
      }
    } finally {
      setIsCreatingInline(false);
    }
  }, [counterpartyQuery, loadCounterparties]);

  const tableRows = useMemo(() => contracts, [contracts]);

  const joinLink = useMemo(() => {
    if (!inviteResult) return null;
    return `${window.location.origin}/join?token=${inviteResult.inviteToken}`;
  }, [inviteResult]);

  const hasExactCounterpartyMatch = useMemo(() => {
    const query = counterpartyQuery.trim().toLowerCase();
    if (!query) return false;
    return counterparties.some((cp) => cp.name.trim().toLowerCase() === query);
  }, [counterpartyQuery, counterparties]);

  const selectedCounterpartyName = useMemo(() => {
    if (!createCounterpartyId) return '';
    return counterparties.find((cp) => cp.id === createCounterpartyId)?.name ?? '';
  }, [createCounterpartyId, counterparties]);

  const canSubmitContract = useMemo(() => {
    return createName.trim().length > 0 && createCounterpartyId.trim().length > 0 && !createLoading;
  }, [createName, createCounterpartyId, createLoading]);

  const onCreateInvite = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setInviteError(null);
      setInviteResult(null);
      setCopyStatus(null);

      if (!tenantId) {
        setInviteError(t.invite.missingTenantId);
        return;
      }

      const email = inviteEmail.trim();
      if (!email.includes('@')) {
        setInviteError(t.common.validationError);
        return;
      }

      setInviteLoading(true);
      try {
        const resp = await createInvite(tenantId, email, Number(inviteRole) as 1 | 2);
        setInviteResult(resp);
      } catch (err: unknown) {
        if (err instanceof ApiError) {
          if (err.status === 400) setInviteError(t.common.validationError);
          else if (err.status === 403) setInviteError(t.common.forbidden);
          else if (err.status === 409) setInviteError(t.invite.alreadyMember);
          else if (err.status >= 500) setInviteError(t.common.serverError);
          else setInviteError(t.common.requestFailed);
        } else {
          setInviteError(t.common.requestFailed);
        }
      } finally {
        setInviteLoading(false);
      }
    },
    [tenantId, inviteEmail, inviteRole]
  );

  const onCopyInviteLink = useCallback(async () => {
    setCopyStatus(null);
    if (!joinLink) return;
    try {
      await navigator.clipboard.writeText(joinLink);
      setCopyStatus(t.common.copied);
    } catch {
      setCopyStatus(t.common.copyFailed);
    }
  }, [joinLink]);

  const onSwitchTenant = useCallback(async () => {
    if (!selectedTenantId) return;
    setSwitchError(null);
    setSwitchSuccess(null);
    setSwitchLoading(true);
    try {
      const resp = await switchTenant(selectedTenantId);
      setToken(resp.token);
      await loadContracts();
      setSwitchSuccess(t.tenant.switchSuccess);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        if (err.status === 403) setSwitchError(t.tenant.switchForbidden);
        else if (err.status === 404) setSwitchError(t.tenant.switchNotFound);
        else if (err.status >= 500) setSwitchError(t.common.serverError);
        else setSwitchError(t.common.requestFailed);
      } else {
        setSwitchError(t.common.requestFailed);
      }
    } finally {
      setSwitchLoading(false);
    }
  }, [selectedTenantId, setToken, loadContracts]);

  return (
    <div className="space-y-4">
      <h2>{t.contracts.title}</h2>

      <div className="card">
        <div>
          {t.contracts.tenantIdFromJwt}: {tenantId ?? t.common.unknown}
        </div>
        <div>
          {t.contracts.roleFromJwt}: {role ?? t.common.unknown}
        </div>
        <button type="button" className="btn-secondary" onClick={() => logout()}>
          {t.common.logout}
        </button>
        <button type="button" className="btn-secondary" onClick={() => void loadContracts()} disabled={loading}>
          {t.common.refresh}
        </button>
        <button type="button" className="btn-primary" onClick={() => setShowCreate((v) => !v)}>
          {t.contracts.createContract}
        </button>
        <button type="button" className="btn-secondary" onClick={() => navigate('/claims')}>
          {t.claims.openPage}
        </button>
        <button type="button" className="btn-secondary" onClick={() => navigate('/imports')}>
          {t.imports.openPage}
        </button>
        {role === 'Admin' ? (
          <button type="button" className="btn-secondary" onClick={() => navigate('/members')}>
            {t.members.openPage}
          </button>
        ) : null}
        {role === 'Admin' ? (
          <button type="button" className="btn-secondary" onClick={() => navigate('/invites')}>
            {t.invites.openPage}
          </button>
        ) : null}
      </div>

      {memberships.length > 1 ? (
        <div style={{ marginTop: 12 }}>
          <label style={{ display: 'block' }}>
            <div>{t.tenant.switchLabel}</div>
            <select
              value={selectedTenantId}
              onChange={(ev) => setSelectedTenantId(ev.target.value)}
              disabled={switchLoading}
            >
              {memberships.map((m) => (
                <option key={m.tenantId} value={m.tenantId}>
                  {m.tenantName} ({m.role})
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="btn-secondary"
            onClick={() => void onSwitchTenant()}
            disabled={switchLoading || !selectedTenantId}
          >
            {switchLoading ? t.tenant.switching : t.tenant.switchButton}
          </button>
          {switchError ? <div style={{ color: 'red' }}>{switchError}</div> : null}
          {switchSuccess ? <div>{switchSuccess}</div> : null}
        </div>
      ) : null}

      {loading ? <div>{t.common.loading}</div> : null}
      {loadError ? <div style={{ color: 'red' }}>{loadError}</div> : null}

      {showCreate ? (
        <form onSubmit={onCreateSubmit} style={{ marginTop: 12, marginBottom: 12 }}>
          <div>
            <label style={{ display: 'block' }}>
              <div>{t.contracts.name}</div>
              <input
                value={createName}
                onChange={(ev) => setCreateName(ev.target.value)}
                disabled={createLoading}
              />
            </label>
          </div>
          <div>
            <div ref={counterpartyDropdownRef}>
              <label style={{ display: 'block' }}>
                <div>{t.contracts.counterparty}</div>
                <input
                  value={counterpartyQuery}
                  onFocus={() => setIsDropdownOpen(true)}
                  onChange={(ev) => {
                    setCounterpartyQuery(ev.target.value);
                    setIsDropdownOpen(true);
                  }}
                  disabled={createLoading || loadingCounterparties}
                  placeholder={selectedCounterpartyName || t.contracts.searchCompany}
                />
              </label>
              <div style={{ fontSize: 12, color: '#666', marginTop: 4 }}>{t.contracts.selectOrCreateCompany}</div>
              {isDropdownOpen ? (
                <div
                  style={{
                    border: '1px solid #ccc',
                    maxHeight: 180,
                    overflowY: 'auto',
                    marginTop: 4
                  }}
                >
                  {filteredCounterparties.map((cp) => (
                    <div
                      key={cp.id}
                      onClick={() => onSelectCounterparty(cp)}
                      style={{ padding: 8, cursor: 'pointer' }}
                      onMouseEnter={(ev) => {
                        ev.currentTarget.style.backgroundColor = '#f5f5f5';
                      }}
                      onMouseLeave={(ev) => {
                        ev.currentTarget.style.backgroundColor = 'transparent';
                      }}
                    >
                      {cp.name}
                    </div>
                  ))}
                  {filteredCounterparties.length === 0 ? (
                    <div style={{ padding: 8 }}>{t.contracts.noResults}</div>
                  ) : null}
                  {counterpartyQuery.trim().length > 0 && !hasExactCounterpartyMatch ? (
                    <div
                      onClick={() => {
                        if (!isCreatingInline) void onCreateCounterpartyInline();
                      }}
                      style={{
                        padding: 8,
                        cursor: isCreatingInline ? 'not-allowed' : 'pointer',
                        color: isCreatingInline ? '#999' : '#0a58ca',
                        borderTop: '1px solid #eee',
                        fontWeight: 600,
                        backgroundColor: isCreatingInline ? '#fafafa' : '#eef5ff'
                      }}
                      onMouseEnter={(ev) => {
                        if (!isCreatingInline) ev.currentTarget.style.backgroundColor = '#deecff';
                      }}
                      onMouseLeave={(ev) => {
                        ev.currentTarget.style.backgroundColor = isCreatingInline ? '#fafafa' : '#eef5ff';
                      }}
                    >
                      {isCreatingInline
                        ? t.common.creating
                        : `+ ${t.contracts.createCompany} "${counterpartyQuery.trim()}"`}
                    </div>
                  ) : null}
                </div>
              ) : null}
            </div>
            {loadingCounterparties ? <div>{t.common.loading}</div> : null}
            {counterpartyError ? <div style={{ color: 'red' }}>{counterpartyError}</div> : null}
          </div>

          <button type="submit" className="btn-primary" disabled={!canSubmitContract}>
            {createLoading ? t.common.creating : t.common.submit}
          </button>
          <button type="button" className="btn-secondary" onClick={() => setShowCreate(false)} disabled={createLoading}>
            {t.common.cancel}
          </button>

          {createError ? <div style={{ color: 'red' }}>{createError}</div> : null}
        </form>
      ) : null}

      <div className="table-wrap">
        <table className="table-base">
        <thead>
          <tr>
            <th>{t.contracts.table.id}</th>
            <th>{t.contracts.table.name}</th>
            <th>{t.contracts.table.counterpartyId}</th>
            <th>{t.contracts.table.status}</th>
            <th>{t.contracts.table.actions}</th>
          </tr>
        </thead>
        <tbody>
          {tableRows.map((c) => (
            <tr key={c.id}>
              <td>{c.id}</td>
              <td>{c.name}</td>
              <td>{c.counterpartyId}</td>
              <td>{c.status ?? ''}</td>
              <td>
                <button type="button" className="btn-secondary" onClick={() => navigate(`/contract/${c.id}`)}>
                  {t.contracts.view}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      </div>

      {/* Admin-only Invite UI */}
      {role === 'Admin' ? (
        <div className="card" style={{ marginTop: 24 }}>
          <h3>{t.invite.inviteUserTitle}</h3>

          <form onSubmit={onCreateInvite}>
            <label style={{ display: 'block' }}>
              <div>{t.invite.email}</div>
              <input
                value={inviteEmail}
                onChange={(ev) => setInviteEmail(ev.target.value)}
                disabled={inviteLoading}
              />
            </label>

            <label style={{ display: 'block' }}>
              <div>{t.invite.role}</div>
              <select
                value={inviteRole}
                onChange={(ev) => setInviteRole(ev.target.value as '1' | '2')}
                disabled={inviteLoading}
              >
                <option value="1">{t.invite.roleMember}</option>
                <option value="2">{t.invite.roleViewer}</option>
              </select>
            </label>

            <button type="submit" className="btn-primary" disabled={inviteLoading}>
              {inviteLoading ? t.common.creating : t.invite.createInvite}
            </button>

            {inviteError ? <div style={{ color: 'red', marginTop: 8 }}>{inviteError}</div> : null}
          </form>

          {inviteResult ? (
            <div style={{ marginTop: 12 }}>
              <div>
                {t.invite.inviteTokenLabel}: <code>{inviteResult.inviteToken}</code>
              </div>
              <div>
                {t.invite.expiresAtLabel}: {inviteResult.expiresAt}
              </div>
              <div>
                {t.invite.joinLinkLabel}:{' '}
                <code>{joinLink}</code>
              </div>
              <button type="button" className="btn-secondary" onClick={() => void onCopyInviteLink()} disabled={!joinLink}>
                {t.invite.copyInviteLink}
              </button>
              {copyStatus ? <div>{copyStatus}</div> : null}
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

