import React, { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError, createTenant } from '../api/api';
import { useAuth } from '../hooks/useAuth';
import { t } from '../i18n';

/**
 * /welcome
 * Bootstrap page for authenticated users with tenant-less JWT (no tenant_id).
 *
 * Allows creating the first company via POST /api/tenants.
 */
export function TenantBootstrapPage() {
  const navigate = useNavigate();
  const { isAuthenticated, tenantId, setToken } = useAuth();

  const [name, setName] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const trimmed = useMemo(() => name.trim(), [name]);
  const isNameValid = trimmed.length >= 2;

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!isAuthenticated) {
      navigate('/login', { replace: true });
      return;
    }

    if (!isNameValid) {
      setError(t.tenant.nameTooShort);
      return;
    }

    setLoading(true);
    try {
      const resp = await createTenant(trimmed);
      setToken(resp.token);
      navigate('/contracts', { replace: true });
    } catch (e2: unknown) {
      if (e2 instanceof ApiError) {
        if (e2.status === 400) setError(t.common.validationError);
        else if (e2.status === 401) setError(t.common.unauthorized);
        else if (e2.status >= 500) setError(t.common.serverError);
        else setError(t.common.requestFailed);
      } else {
        setError(t.common.requestFailed);
      }
    } finally {
      setLoading(false);
    }
  };

  // If user already has a tenant, this page is not needed.
  if (isAuthenticated && tenantId) {
    return (
      <div>
        <div>{t.tenant.alreadyInCompany}</div>
        <button type="button" onClick={() => navigate('/contracts', { replace: true })}>
          {t.tenant.goToContracts}
        </button>
      </div>
    );
  }

  return (
    <div>
      <h2>{t.tenant.welcomeTitle}</h2>
      <div>{t.tenant.noCompanyYet}</div>

      <form onSubmit={onSubmit} style={{ marginTop: 12 }}>
        <label style={{ display: 'block' }}>
          <div>{t.tenant.companyName}</div>
          <input
            value={name}
            onChange={(ev) => setName(ev.target.value)}
            disabled={loading}
            placeholder={t.tenant.companyNamePlaceholder}
          />
        </label>

        <button type="submit" disabled={loading || !isAuthenticated}>
          {loading ? t.tenant.creatingCompany : t.tenant.createCompany}
        </button>

        {error ? <div style={{ color: 'red', marginTop: 12 }}>{error}</div> : null}
        {!isAuthenticated ? (
          <div style={{ color: 'red', marginTop: 12 }}>{t.tenant.pleaseLoginFirst}</div>
        ) : null}
      </form>
    </div>
  );
}

