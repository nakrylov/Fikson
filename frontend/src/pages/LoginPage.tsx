import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Input } from '../components/Input';
import { ApiError } from '../api/api';
import { useAuth } from '../hooks/useAuth';

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
        setError('Invalid credentials');
      } else {
        setError('Invalid credentials');
        console.error(err);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <h2>Fixon Login</h2>

      {isAuthenticated ? (
        <div>
          <div>Authenticated</div>
          <div>tenantId: {tenantId ?? '(unknown)'}</div>
          <div>role: {role ?? '(unknown)'}</div>
          <Button type="button" onClick={() => logout()} label="Logout" />
          <Button type="button" onClick={() => navigate('/contracts')} label="Go to contracts" />
        </div>
      ) : (
        <form onSubmit={onSubmit}>
          <Input
            label="Email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
          />
          <Input
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
          />

          <Button type="submit" disabled={loading} label={loading ? 'Signing in…' : 'Sign in'} />

          {error ? <div style={{ color: 'red' }}>{error}</div> : null}
        </form>
      )}
    </div>
  );
}

