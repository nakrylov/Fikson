import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
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
      navigate('/contracts');
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
          <Button type="button" onClick={() => logout()} label={t.common.logout} />
          <Button type="button" onClick={() => navigate('/contracts')} label={t.auth.goToContracts} />
        </div>
      ) : (
        <form onSubmit={onSubmit}>
          <Input
            label={t.auth.email}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
          />
          <Input
            label={t.auth.password}
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
          />

          <Button
            type="submit"
            disabled={loading}
            label={loading ? t.auth.signingIn : t.auth.signIn}
          />

          {error ? <div style={{ color: 'red' }}>{error}</div> : null}
        </form>
      )}
    </div>
  );
}

