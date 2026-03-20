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
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { FormField } from '../components/FormField';
import { Input } from '../components/Input';
import { Column, Table } from '../components/Table';
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
  const contractColumns = useMemo<Column<Contract>[]>(() => [
    { key: 'id', title: t.contracts.table.id },
    { key: 'name', title: t.contracts.table.name },
    { key: 'counterpartyId', title: t.contracts.table.counterpartyId },
    { key: 'status', title: t.contracts.table.status },
    {
      key: 'actions',
      title: t.contracts.table.actions,
      render: (contract) => (
        <Button type="button" variant="secondary" onClick={() => navigate(`/contract/${contract.id}`)}>
          {t.contracts.view}
        </Button>
      )
    }
  ], [navigate]);

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
  const createNameFieldError = createError && !createName.trim() ? createError : undefined;
  const createCounterpartyFieldError =
    counterpartyError ?? (createError && !createCounterpartyId.trim() ? createError : undefined);
  const inviteEmailFieldError = inviteError ?? undefined;

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
    <div className="space-y-6">
      <Card
        title={t.contracts.title}
        actions={(
          <div className="flex gap-2 flex-wrap">
            <Button type="button" variant="secondary" onClick={() => void loadContracts()} disabled={loading}>
              {t.common.refresh}
            </Button>
            <Button type="button" variant="primary" onClick={() => setShowCreate((v) => !v)}>
              {t.contracts.createContract}
            </Button>
          </div>
        )}
      >
        <div>
          {t.contracts.tenantIdFromJwt}: {tenantId ?? t.common.unknown}
        </div>
        <div>
          {t.contracts.roleFromJwt}: {role ?? t.common.unknown}
        </div>
        <div className="flex gap-2 flex-wrap mt-3">
          <Button type="button" variant="secondary" onClick={() => logout()}>
            {t.common.logout}
          </Button>
          <Button type="button" variant="secondary" onClick={() => navigate('/claims')}>
            {t.claims.openPage}
          </Button>
          <Button type="button" variant="secondary" onClick={() => navigate('/imports')}>
            {t.imports.openPage}
          </Button>
          {role === 'Admin' ? (
            <Button type="button" variant="secondary" onClick={() => navigate('/members')}>
              {t.members.openPage}
            </Button>
          ) : null}
          {role === 'Admin' ? (
            <Button type="button" variant="secondary" onClick={() => navigate('/invites')}>
              {t.invites.openPage}
            </Button>
          ) : null}
        </div>
      </Card>

      {memberships.length > 1 ? (
        <Card title={t.tenant.switchLabel}>
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
          <div className="flex gap-2 mt-2">
            <Button
              type="button"
              variant="secondary"
              onClick={() => void onSwitchTenant()}
              disabled={switchLoading || !selectedTenantId}
            >
              {switchLoading ? t.tenant.switching : t.tenant.switchButton}
            </Button>
          </div>
          {switchError ? <div style={{ color: 'red' }}>{switchError}</div> : null}
          {switchSuccess ? <div>{switchSuccess}</div> : null}
        </Card>
      ) : null}

      {loading ? <div>{t.common.loading}</div> : null}
      {loadError ? <div style={{ color: 'red' }}>{loadError}</div> : null}

      {showCreate ? (
        <Card title={t.contracts.createContract}>
          <form onSubmit={onCreateSubmit} className="space-y-4">
            <FormField label={t.contracts.name} error={createNameFieldError}>
              <Input
                value={createName}
                onChange={(ev) => setCreateName(ev.target.value)}
                disabled={createLoading}
                error={Boolean(createNameFieldError)}
              />
            </FormField>
            <div>
              <div ref={counterpartyDropdownRef}>
                <FormField label={t.contracts.counterparty} error={createCounterpartyFieldError}>
                  <Input
                    value={counterpartyQuery}
                    onFocus={() => setIsDropdownOpen(true)}
                    onChange={(ev) => {
                      setCounterpartyQuery(ev.target.value);
                      setIsDropdownOpen(true);
                    }}
                    disabled={createLoading || loadingCounterparties}
                    placeholder={selectedCounterpartyName || t.contracts.searchCompany}
                    error={Boolean(createCounterpartyFieldError)}
                  />
                </FormField>
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
            </div>

            <div className="flex gap-2 mt-2">
              <Button type="submit" variant="primary" disabled={!canSubmitContract}>
                {createLoading ? t.common.creating : t.common.submit}
              </Button>
              <Button type="button" variant="secondary" onClick={() => setShowCreate(false)} disabled={createLoading}>
                {t.common.cancel}
              </Button>
            </div>

            {createError ? <div style={{ color: 'red' }}>{createError}</div> : null}
          </form>
        </Card>
      ) : null}

      <Card title={t.contracts.title}>
        <Table columns={contractColumns} data={tableRows} emptyText={t.common.noData} />
      </Card>

      {/* Admin-only Invite UI */}
      {role === 'Admin' ? (
        <Card title={t.invite.inviteUserTitle}>
          <form onSubmit={onCreateInvite} className="space-y-4">
            <FormField label={t.invite.email} error={inviteEmailFieldError}>
              <Input
                value={inviteEmail}
                onChange={(ev) => setInviteEmail(ev.target.value)}
                disabled={inviteLoading}
                error={Boolean(inviteEmailFieldError)}
              />
            </FormField>

            <FormField label={t.invite.role}>
              <select
                value={inviteRole}
                onChange={(ev) => setInviteRole(ev.target.value as '1' | '2')}
                disabled={inviteLoading}
              >
                <option value="1">{t.invite.roleMember}</option>
                <option value="2">{t.invite.roleViewer}</option>
              </select>
            </FormField>

            <div className="flex gap-2 mt-2">
              <Button type="submit" variant="primary" disabled={inviteLoading}>
                {inviteLoading ? t.common.creating : t.invite.createInvite}
              </Button>
            </div>

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
              <Button type="button" variant="secondary" onClick={() => void onCopyInviteLink()} disabled={!joinLink}>
                {t.invite.copyInviteLink}
              </Button>
              {copyStatus ? <div>{copyStatus}</div> : null}
            </div>
          ) : null}
        </Card>
      ) : null}
    </div>
  );
}

