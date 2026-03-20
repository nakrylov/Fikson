import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { FormField } from '../components/FormField';
import { Input } from '../components/Input';
import { ApiError } from '../api/api';
import { useAuth } from '../hooks/useAuth';
import { t } from '../i18n';

/**
 * /login
 * Minimal login page: email + password → POST /api/auth/login → store JWT.
 */
export function LoginPage() {
  const { login, isAuthenticated, tenantId, role, logout } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(email, password);
      navigate('/dashboard');
    } catch (err: unknown) {
      // Requirement: show "Invalid credentials" on error.
      // (We intentionally keep it simple, but still handle non-401 errors gracefully.)
      if (err instanceof ApiError && (err.status === 401 || err.status === 400)) {
        setError(t.auth.invalidCredentials);
      } else {
        setError(t.auth.invalidCredentials);
        console.error(err);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <h2>{t.auth.loginTitle}</h2>

      {isAuthenticated ? (
        <div>
          <div>{t.auth.authenticated}</div>
          <div>
            {t.auth.tenantIdLabel}: {tenantId ?? t.common.unknown}
          </div>
          <div>
            {t.auth.roleLabel}: {role ?? t.common.unknown}
          </div>
          <div className="flex gap-2 mt-2">
            <Button type="button" variant="secondary" onClick={() => logout()}>
              {t.common.logout}
            </Button>
            <Button type="button" variant="primary" onClick={() => navigate('/dashboard')}>
              {t.auth.goToContracts}
            </Button>
          </div>
        </div>
      ) : (
        <form onSubmit={onSubmit} className="space-y-4">
          <FormField label={t.auth.email} error={error ?? undefined}>
            <Input
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
              error={Boolean(error)}
            />
          </FormField>
          <FormField label={t.auth.password} error={error ?? undefined}>
            <Input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              error={Boolean(error)}
            />
          </FormField>

          <Button type="submit" variant="primary" disabled={loading}>
            {loading ? t.auth.signingIn : t.auth.signIn}
          </Button>
        </form>
      )}
    </div>
  );
}

