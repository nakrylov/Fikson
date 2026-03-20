import React, { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { acceptInvite, ApiError, apiRequest, getInvite, InviteInfo } from '../api/api';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { FormField } from '../components/FormField';
import { Input } from '../components/Input';
import { useAuth } from '../hooks/useAuth';
import { t } from '../i18n';

type RegisterResponse = {
  token: string;
};

/**
 * /join?token=XYZ
 *
 * Variant A flow:
 * - Load invite metadata (public GET /api/invites/{token})
 * - If not authenticated: register, then auto-accept invite
 * - If authenticated: allow manual accept
 * - On success: store tenant JWT and redirect to /contracts
 */
export function JoinInvitePage() {
  const location = useLocation();
  const navigate = useNavigate();
  const { isAuthenticated, setToken } = useAuth();

  const token = useMemo(() => {
    const params = new URLSearchParams(location.search);
    return params.get('token')?.trim() ?? '';
  }, [location.search]);

  const [loadingInvite, setLoadingInvite] = useState<boolean>(true);
  const [invite, setInvite] = useState<InviteInfo | null>(null);
  const [inviteError, setInviteError] = useState<string | null>(null);

  const [email, setEmail] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [warning, setWarning] = useState<string | null>(null);

  const [actionLoading, setActionLoading] = useState<boolean>(false);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      setLoadingInvite(true);
      setInvite(null);
      setInviteError(null);

      if (!token) {
        setInviteError(t.invite.invalidInvite);
        setLoadingInvite(false);
        return;
      }

      try {
        const data = await getInvite(token);
        if (cancelled) return;
        setInvite(data);
        setEmail(data.email);
      } catch (e: unknown) {
        if (cancelled) return;
        if (e instanceof ApiError) {
          if (e.status === 404) setInviteError(t.invite.invalidInvite);
          else if (e.status === 410) setInviteError(t.invite.expiredInvite);
          else if (e.status === 409) setInviteError(t.invite.inviteAlreadyAccepted);
          else if (e.status >= 500) setInviteError(t.common.serverError);
          else setInviteError(t.common.requestFailed);
        } else {
          setInviteError(t.common.requestFailed);
        }
      } finally {
        if (!cancelled) setLoadingInvite(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [token]);

  useEffect(() => {
    if (!invite) {
      setWarning(null);
      return;
    }

    if (email.trim().toLowerCase() !== invite.email.trim().toLowerCase()) {
      setWarning(t.invite.warningDifferentEmail);
    } else {
      setWarning(null);
    }
  }, [email, invite]);

  const doAccept = async () => {
    setActionError(null);
    setActionLoading(true);
    try {
      const resp = await acceptInvite(token);
      setToken(resp.token);
      navigate('/contracts', { replace: true });
    } catch (e: unknown) {
      if (e instanceof ApiError) {
        if (e.status === 403) {
          setActionError(t.invite.differentEmailError);
        } else if (e.status === 404) {
          setActionError(t.invite.invalidInvite);
        } else if (e.status === 410) {
          setActionError(t.invite.expiredInvite);
        } else if (e.status === 409) {
          setActionError(t.invite.inviteAlreadyAccepted);
        } else if (e.status >= 500) {
          setActionError(t.common.serverError);
        } else {
          setActionError(t.common.requestFailed);
        }
      } else {
        setActionError(t.common.requestFailed);
      }
    } finally {
      setActionLoading(false);
    }
  };

  const onRegisterAndJoin = async (e: React.FormEvent) => {
    e.preventDefault();
    setActionError(null);
    setActionLoading(true);

    try {
      // Register (public)
      const reg = await apiRequest<RegisterResponse>('/api/auth/register', {
        method: 'POST',
        skipAuth: true,
        body: { email, password }
      });

      setToken(reg.token);

      // Auto-accept invite
      await doAccept();
    } catch (e2: unknown) {
      if (e2 instanceof ApiError) {
        if (e2.status === 400) {
          setActionError(t.common.validationError);
        } else if (e2.status >= 500) {
          setActionError(t.common.serverError);
        } else {
          setActionError(t.common.requestFailed);
        }
      } else {
        setActionError(t.common.requestFailed);
      }
    } finally {
      setActionLoading(false);
    }
  };

  if (loadingInvite) {
    return (
      <div className="space-y-6">
        <Card title={t.invite.joinTenantTitle}>
          <div>{t.common.loading}</div>
        </Card>
      </div>
    );
  }

  if (inviteError) {
    return (
      <div className="space-y-6">
        <Card title={t.invite.joinTenantTitle}>
          <div style={{ color: 'red' }}>{inviteError}</div>
        </Card>
      </div>
    );
  }

  if (!invite) {
    return (
      <div className="space-y-6">
        <Card title={t.invite.joinTenantTitle}>
          <div style={{ color: 'red' }}>{t.invite.invalidInvite}</div>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <Card title={t.invite.joinTenantTitle}>
        <div className="space-y-1">
          <div>
            {t.invite.invitedToJoinPrefix} <strong>{invite.tenantName}</strong>
          </div>
          <div>
            {t.invite.email}: <strong>{invite.email}</strong>
          </div>
          <div>
            {t.invite.role}: <strong>{invite.role}</strong>
          </div>
          <div>
            {t.invite.expiresAtLabel}: {invite.expiresAt}
          </div>
        </div>
      </Card>

      {!isAuthenticated ? (
        <Card title={t.invite.registerToAccept}>
          <form onSubmit={onRegisterAndJoin} className="space-y-4">
            <FormField label={t.invite.email} error={actionError ?? undefined}>
              <Input
                value={email}
                onChange={(ev) => setEmail(ev.target.value)}
                disabled={actionLoading}
                error={Boolean(actionError)}
              />
            </FormField>

            {warning ? <div style={{ color: 'orange' }}>{warning}</div> : null}

            <FormField label={t.auth.password} error={actionError ?? undefined}>
              <Input
                type="password"
                value={password}
                onChange={(ev) => setPassword(ev.target.value)}
                disabled={actionLoading}
                error={Boolean(actionError)}
              />
            </FormField>

            <Button type="submit" variant="primary" disabled={actionLoading}>
              {actionLoading ? t.common.working : t.invite.registerAndJoin}
            </Button>
          </form>
        </Card>
      ) : (
        <Card title={`${t.invite.joinHeadingPrefix} ${invite.tenantName}`}>
          <Button type="button" variant="primary" onClick={() => void doAccept()} disabled={actionLoading}>
            {actionLoading ? t.common.joining : t.invite.acceptInvite}
          </Button>
        </Card>
      )}

      {actionError ? (
        <Card title={t.common.requestFailed}>
          <div style={{ color: 'red' }}>{actionError}</div>
        </Card>
      ) : null}
    </div>
  );
}

