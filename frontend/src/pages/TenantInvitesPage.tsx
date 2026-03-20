import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { ApiError, getTenantInvites, revokeTenantInvite, TenantInvite } from '../api/api';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { Column, Table } from '../components/Table';
import { useAuth } from '../hooks/useAuth';
import { t } from '../i18n';

export function TenantInvitesPage() {
  const { tenantId, role } = useAuth();
  const [items, setItems] = useState<TenantInvite[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [actionStatus, setActionStatus] = useState<string | null>(null);
  const [revokeLoadingId, setRevokeLoadingId] = useState<string | null>(null);

  const loadInvites = useCallback(async () => {
    if (!tenantId || role !== 'Admin') return;
    setLoading(true);
    setError(null);
    try {
      const data = await getTenantInvites(tenantId);
      setItems(data);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        if (err.status === 403) setError(t.invites.forbidden);
        else if (err.status >= 500) setError(t.common.serverError);
        else setError(t.common.requestFailed);
      } else {
        setError(t.common.requestFailed);
      }
    } finally {
      setLoading(false);
    }
  }, [tenantId, role]);

  useEffect(() => {
    void loadInvites();
  }, [loadInvites]);

  const rows = useMemo(() => items, [items]);

  const onCopyLink = useCallback(async (token: string) => {
    setActionStatus(null);
    const link = `${window.location.origin}/join?token=${token}`;
    try {
      await navigator.clipboard.writeText(link);
      setActionStatus(t.invites.copySuccess);
    } catch {
      setActionStatus(t.common.copyFailed);
    }
  }, []);

  const onRevoke = useCallback(
    async (inviteId: string) => {
      if (!tenantId) return;
      setActionStatus(null);
      setRevokeLoadingId(inviteId);
      try {
        await revokeTenantInvite(tenantId, inviteId);
        await loadInvites();
        setActionStatus(t.invites.revokeSuccess);
      } catch (err: unknown) {
        if (err instanceof ApiError) {
          if (err.status === 403) setActionStatus(t.invites.forbidden);
          else setActionStatus(t.invites.revokeError);
        } else {
          setActionStatus(t.invites.revokeError);
        }
      } finally {
        setRevokeLoadingId(null);
      }
    },
    [tenantId, loadInvites]
  );
  const inviteColumns = useMemo<Column<TenantInvite>[]>(() => [
    { key: 'email', title: t.invites.email },
    { key: 'role', title: t.invites.role },
    { key: 'status', title: t.invites.status },
    { key: 'expiresAt', title: t.invites.expiresAt },
    {
      key: 'actions',
      title: t.invites.actions,
      render: (invite) =>
        invite.status === 'Pending' ? (
          <div className="flex gap-2">
            <Button type="button" variant="secondary" onClick={() => void onCopyLink(invite.token)}>
              {t.invites.copyLink}
            </Button>
            <Button
              type="button"
              variant="danger"
              onClick={() => void onRevoke(invite.id)}
              disabled={revokeLoadingId === invite.id}
            >
              {t.invites.revoke}
            </Button>
          </div>
        ) : null
    }
  ], [onCopyLink, onRevoke, revokeLoadingId]);

  if (!tenantId) {
    return <Navigate to="/welcome" replace />;
  }

  if (role !== 'Admin') {
    return (
      <div className="space-y-6">
        <Card title={t.invites.title}>
        <div style={{ color: 'red' }}>{t.invites.forbidden}</div>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <Card title={t.invites.title}>
        {loading ? <div>{t.common.loading}</div> : null}
        {error ? <div style={{ color: 'red' }}>{error}</div> : null}
        {actionStatus ? <div>{actionStatus}</div> : null}
        {!loading && !error ? <Table columns={inviteColumns} data={rows} emptyText={t.invites.empty} /> : null}
      </Card>
    </div>
  );
}
