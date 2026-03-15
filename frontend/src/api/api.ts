/**
 * Minimal API wrapper over `fetch`.
 *
 * Responsibilities:
 * - Adds `Authorization: Bearer <jwt>` automatically (if token exists).
 * - JSON request/response helpers.
 * - Handles 401 globally (clear JWT + redirect to /login).
 *
 * This is intentionally small and framework-agnostic.
 */

export type ApiRequestInit = Omit<RequestInit, 'body'> & {
  body?: unknown;
  /** Skip adding Authorization header (e.g. for /auth/login). */
  skipAuth?: boolean;
};

export type ApiErrorPayload = unknown;

export class ApiError extends Error {
  public readonly status: number;
  public readonly payload: ApiErrorPayload | null;

  constructor(message: string, status: number, payload: ApiErrorPayload | null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.payload = payload;
  }
}

const STORAGE_KEY = 'fixon.jwt';

export function getStoredToken(): string | null {
  return localStorage.getItem(STORAGE_KEY);
}

export function setStoredToken(token: string): void {
  localStorage.setItem(STORAGE_KEY, token);
}

export function clearStoredToken(): void {
  localStorage.removeItem(STORAGE_KEY);
}

function getApiBaseUrl(): string {
  // If VITE_API_BASE_URL is empty/undefined, use relative URLs.
  return (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '');
}

export async function apiRequest<T = unknown>(path: string, init: ApiRequestInit = {}): Promise<T> {
  // Requirement: use VITE_API_BASE_URL if set, otherwise call relative `/api/*`.
  const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '');
  const url = `${baseUrl}${path.startsWith('/') ? path : `/${path}`}`;

  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');

  if (!init.skipAuth) {
    const token = getStoredToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
  }

  let body: BodyInit | undefined;
  if (init.body !== undefined) {
    headers.set('Content-Type', 'application/json');
    body = JSON.stringify(init.body);
  }

  const res = await fetch(url, {
    ...init,
    headers,
    body
  });

  if (res.status === 401) {
    // Global 401 handling: clear token and redirect to login.
    clearStoredToken();
    if (window.location.pathname !== '/login') window.location.href = '/login';
    throw new ApiError('Unauthorized', 401, null);
  }

  if (res.status === 204) {
    // No content.
    return undefined as T;
  }

  // Try to parse JSON; if not JSON, return text.
  const contentType = res.headers.get('content-type') ?? '';
  const isJson = contentType.includes('application/json');
  const payload = isJson ? await res.json().catch(() => null) : await res.text().catch(() => null);

  if (!res.ok) {
    throw new ApiError(`Request failed (${res.status})`, res.status, payload);
  }

  return payload as T;
}

// -----------------------
// Invite Flow helpers
// -----------------------

export type InviteInfo = {
  tenantName: string;
  email: string;
  role: string;
  expiresAt: string;
};

export async function getInvite(token: string): Promise<InviteInfo> {
  // Public endpoint.
  return apiRequest<InviteInfo>(`/api/invites/${encodeURIComponent(token)}`, { method: 'GET', skipAuth: true });
}

export type AcceptInviteResponse = {
  token: string;
  tenantId: string;
  role: string;
};

export async function acceptInvite(token: string): Promise<AcceptInviteResponse> {
  // Authenticated endpoint (Authorization header is added automatically from localStorage).
  return apiRequest<AcceptInviteResponse>('/api/tenants/join', {
    method: 'POST',
    body: { token }
  });
}

export type CreateTenantResponse = {
  token: string;
  tenantId: string;
  role: string;
};

export async function createTenant(name: string): Promise<CreateTenantResponse> {
  return apiRequest<CreateTenantResponse>('/api/tenants', {
    method: 'POST',
    body: { name }
  });
}

export type CreateInviteResponse = {
  inviteToken: string;
  expiresAt: string;
};

export async function createInvite(tenantId: string, email: string, role: string): Promise<CreateInviteResponse> {
  return apiRequest<CreateInviteResponse>(`/api/tenants/${encodeURIComponent(tenantId)}/invites`, {
    method: 'POST',
    body: { email, role }
  });
}

export type CurrentUserMembership = {
  tenantId: string;
  tenantName: string;
  role: string;
};

export type CurrentUserResponse = {
  userId: string;
  email: string;
  currentTenantId: string | null;
  role: string | null;
  memberships: CurrentUserMembership[];
};

export async function getCurrentUser(): Promise<CurrentUserResponse> {
  return apiRequest<CurrentUserResponse>('/api/auth/me', {
    method: 'GET'
  });
}

export type SwitchTenantResponse = {
  token: string;
  tenantId: string;
  role: string;
};

type RawSwitchTenantResponse = {
  token: string;
  tenantId: string;
  role?: string;
  roles?: string[];
};

export async function switchTenant(tenantId: string): Promise<SwitchTenantResponse> {
  const raw = await apiRequest<RawSwitchTenantResponse>('/api/auth/switch-tenant', {
    method: 'POST',
    body: { tenantId }
  });

  return {
    token: raw.token,
    tenantId: raw.tenantId,
    role: raw.role ?? raw.roles?.[0] ?? ''
  };
}

export type TenantMember = {
  userId: string;
  email: string;
  role: string;
  status: string;
  createdAt: string;
};

export async function getTenantMembers(tenantId: string): Promise<TenantMember[]> {
  return apiRequest<TenantMember[]>(`/api/tenants/${encodeURIComponent(tenantId)}/members`, {
    method: 'GET'
  });
}

export type TenantInvite = {
  id: string;
  email: string;
  role: string;
  status: string;
  token: string;
  createdAt: string;
  expiresAt: string;
};

export async function getTenantInvites(tenantId: string): Promise<TenantInvite[]> {
  return apiRequest<TenantInvite[]>(`/api/tenants/${encodeURIComponent(tenantId)}/invites`, {
    method: 'GET'
  });
}

export async function revokeTenantInvite(tenantId: string, inviteId: string): Promise<void> {
  await apiRequest<void>(`/api/tenants/${encodeURIComponent(tenantId)}/invites/${encodeURIComponent(inviteId)}/revoke`, {
    method: 'POST'
  });
}

