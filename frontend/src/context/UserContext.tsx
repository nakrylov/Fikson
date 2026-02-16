import React, { createContext, useCallback, useEffect, useMemo, useState } from 'react';
import { apiRequest, clearStoredToken, getStoredToken, setStoredToken } from '../api/api';

/**
 * UserContext:
 * - Stores JWT and derived claims (tenantId, role).
 * - Provides login/logout methods.
 * - Rehydrates state from localStorage on startup.
 *
 * NOTE:
 * The backend's login currently returns a JWT token; we parse claims locally.
 */

export type UserInfo = {
  token: string | null;
  tenantId: string | null;
  role: string | null;
};

export type UserContextValue = UserInfo & {
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
};

const UserContext = createContext<UserContextValue | undefined>(undefined);

function base64UrlDecode(input: string): string {
  // Base64url → base64
  const pad = '='.repeat((4 - (input.length % 4)) % 4);
  const b64 = (input + pad).replace(/-/g, '+').replace(/_/g, '/');
  return decodeURIComponent(
    atob(b64)
      .split('')
      .map((c) => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`)
      .join('')
  );
}

type JwtPayload = Record<string, unknown>;

function tryParseJwtPayload(token: string): JwtPayload | null {
  const parts = token.split('.');
  if (parts.length < 2) return null;
  try {
    const json = base64UrlDecode(parts[1]);
    return JSON.parse(json) as JwtPayload;
  } catch {
    return null;
  }
}

function extractTenantId(payload: JwtPayload | null): string | null {
  const v = payload?.['tenant_id'];
  return typeof v === 'string' ? v : null;
}

function extractRole(payload: JwtPayload | null): string | null {
  // Depending on claim mapping, role might be:
  // - "role"
  // - "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
  const direct =
    payload?.['role'] ??
    payload?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
    payload?.['roles'];

  if (typeof direct === 'string') return direct;
  if (Array.isArray(direct) && typeof direct[0] === 'string') return direct[0];
  return null;
}

type LoginResponse = {
  token: string;
};

export function UserProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [tenantId, setTenantId] = useState<string | null>(null);
  const [role, setRole] = useState<string | null>(null);

  const isAuthenticated = !!token;

  const hydrateFromToken = useCallback((jwt: string | null) => {
    setToken(jwt);
    const payload = jwt ? tryParseJwtPayload(jwt) : null;
    setTenantId(extractTenantId(payload));
    setRole(extractRole(payload));
  }, []);

  useEffect(() => {
    // On app start: restore token from localStorage (if any).
    const stored = getStoredToken();
    hydrateFromToken(stored);
  }, [hydrateFromToken]);

  const login = useCallback(
    async (email: string, password: string) => {
      // /api/auth/login is public: no Authorization header.
      const resp = await apiRequest<LoginResponse>('/api/auth/login', {
        method: 'POST',
        skipAuth: true,
        body: { email, password }
      });

      setStoredToken(resp.token);
      hydrateFromToken(resp.token);
    },
    [hydrateFromToken]
  );

  const logout = useCallback(() => {
    clearStoredToken();
    hydrateFromToken(null);
  }, [hydrateFromToken]);

  const value = useMemo<UserContextValue>(
    () => ({
      token,
      tenantId,
      role,
      isAuthenticated,
      login,
      logout
    }),
    [token, tenantId, role, isAuthenticated, login, logout]
  );

  return <UserContext.Provider value={value}>{children}</UserContext.Provider>;
}

export function useUserContext(): UserContextValue {
  const ctx = React.useContext(UserContext);
  if (!ctx) {
    throw new Error('useUserContext must be used within UserProvider');
  }
  return ctx;
}

