import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { ApiError, getTenantInvites, revokeTenantInvite, TenantInvite } from '../api/api';
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

  if (!tenantId) {
    return <Navigate to="/welcome" replace />;
  }

  if (role !== 'Admin') {
    return (
      <div>
        <h2>{t.invites.title}</h2>
        <div style={{ color: 'red' }}>{t.invites.forbidden}</div>
      </div>
    );
  }

  return (
    <div>
      <h2>{t.invites.title}</h2>

      {loading ? <div>{t.common.loading}</div> : null}
      {error ? <div style={{ color: 'red' }}>{error}</div> : null}
      {actionStatus ? <div>{actionStatus}</div> : null}

      {!loading && !error && rows.length === 0 ? <div>{t.invites.empty}</div> : null}

      {!loading && !error && rows.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>{t.invites.email}</th>
              <th>{t.invites.role}</th>
              <th>{t.invites.status}</th>
              <th>{t.invites.expiresAt}</th>
              <th>{t.invites.actions}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((invite) => {
              const isPending = invite.status === 'Pending';
              return (
                <tr key={invite.id}>
                  <td>{invite.email}</td>
                  <td>{invite.role}</td>
                  <td>{invite.status}</td>
                  <td>{invite.expiresAt}</td>
                  <td>
                    {isPending ? (
                      <>
                        <button type="button" onClick={() => void onCopyLink(invite.token)}>
                          {t.invites.copyLink}
                        </button>
                        <button
                          type="button"
                          onClick={() => void onRevoke(invite.id)}
                          disabled={revokeLoadingId === invite.id}
                        >
                          {t.invites.revoke}
                        </button>
                      </>
                    ) : null}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
