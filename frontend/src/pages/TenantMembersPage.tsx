import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ApiError, getTenantMembers, TenantMember } from '../api/api';
import { useAuth } from '../hooks/useAuth';
import { t } from '../i18n';

export function TenantMembersPage() {
  const { tenantId, role } = useAuth();
  const navigate = useNavigate();
  const [items, setItems] = useState<TenantMember[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const loadMembers = useCallback(async () => {
    if (!tenantId || role !== 'Admin') return;
    setLoading(true);
    setError(null);
    try {
      const data = await getTenantMembers(tenantId);
      setItems(data);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        if (err.status === 403) setError(t.members.forbidden);
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
    void loadMembers();
  }, [loadMembers]);

  const rows = useMemo(() => items, [items]);

  if (!tenantId) {
    return <Navigate to="/welcome" replace />;
  }

  if (role !== 'Admin') {
    return (
      <div>
        <h2>{t.members.title}</h2>
        <div style={{ color: 'red' }}>{t.members.forbidden}</div>
      </div>
    );
  }

  return (
    <div>
      <h2>{t.members.title}</h2>
      <button type="button" onClick={() => navigate('/invites')}>
        {t.invites.openPage}
      </button>

      {loading ? <div>{t.common.loading}</div> : null}
      {error ? <div style={{ color: 'red' }}>{error}</div> : null}

      {!loading && !error && rows.length === 0 ? <div>{t.members.empty}</div> : null}

      {!loading && !error && rows.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>{t.members.email}</th>
              <th>{t.members.role}</th>
              <th>{t.members.status}</th>
              <th>{t.members.createdAt}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((m) => (
              <tr key={`${m.userId}-${m.email}`}>
                <td>{m.email}</td>
                <td>{m.role}</td>
                <td>{m.status}</td>
                <td>{m.createdAt}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
