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

