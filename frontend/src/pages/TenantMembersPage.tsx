import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ApiError, getTenantMembers, TenantMember } from '../api/api';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { Column, Table } from '../components/Table';
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
  const memberColumns = useMemo<Column<TenantMember>[]>(() => [
    { key: 'email', title: t.members.email },
    { key: 'role', title: t.members.role },
    { key: 'status', title: t.members.status },
    { key: 'createdAt', title: t.members.createdAt }
  ], []);

  if (!tenantId) {
    return <Navigate to="/welcome" replace />;
  }

  if (role !== 'Admin') {
    return (
      <div className="space-y-6">
        <Card title={t.members.title}>
        <div style={{ color: 'red' }}>{t.members.forbidden}</div>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <Card
        title={t.members.title}
        actions={(
          <Button type="button" variant="secondary" onClick={() => navigate('/invites')}>
            {t.invites.openPage}
          </Button>
        )}
      >
        {loading ? <div>{t.common.loading}</div> : null}
        {error ? <div style={{ color: 'red' }}>{error}</div> : null}
        {!loading && !error ? <Table columns={memberColumns} data={rows} emptyText={t.members.empty} /> : null}
      </Card>
    </div>
  );
}
