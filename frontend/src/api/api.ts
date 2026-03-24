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

export type Counterparty = {
  id: string;
  name: string;
};

export async function getCounterparties(): Promise<Counterparty[]> {
  return apiRequest<Counterparty[]>('/api/counterparties', {
    method: 'GET'
  });
}

export async function createCounterparty(name: string): Promise<Counterparty> {
  return apiRequest<Counterparty>('/api/counterparties', {
    method: 'POST',
    body: { name }
  });
}

export type CreateInviteResponse = {
  inviteToken: string;
  expiresAt: string;
};

export type MembershipRoleId = 0 | 1 | 2;

export async function createInvite(tenantId: string, email: string, role: MembershipRoleId): Promise<CreateInviteResponse> {
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

export type ContractDetailsResponse = {
  id: string;
  name: string;
  counterpartyId: string;
  status: number | string;
  createdAt: string;
  currentVersionId?: string | null;
};

export type ContractDashboard = {
  totalClaims: number;
  totalPenalty: number;
  shipmentsAffected: number;
  lastImportDate: string | null;
};

export async function getContractDashboard(contractId: string): Promise<ContractDashboard> {
  return apiRequest<ContractDashboard>(`/api/contracts/${encodeURIComponent(contractId)}/dashboard`, {
    method: 'GET'
  });
}

export async function getContractDetailsWithEtag(
  contractId: string
): Promise<{ contract: ContractDetailsResponse; etag: string | null }> {
  const baseUrl = getApiBaseUrl();
  const url = `${baseUrl}/api/contracts/${encodeURIComponent(contractId)}`;

  const headers = new Headers();
  headers.set('Accept', 'application/json');

  const token = getStoredToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const res = await fetch(url, { method: 'GET', headers });

  if (res.status === 401) {
    clearStoredToken();
    if (window.location.pathname !== '/login') window.location.href = '/login';
    throw new ApiError('Unauthorized', 401, null);
  }

  const contentType = res.headers.get('content-type') ?? '';
  const isJson = contentType.includes('application/json');
  const payload = isJson ? await res.json().catch(() => null) : await res.text().catch(() => null);

  if (!res.ok) {
    throw new ApiError(`Request failed (${res.status})`, res.status, payload);
  }

  return {
    contract: payload as ContractDetailsResponse,
    etag: res.headers.get('ETag')
  };
}

export async function updateContractName(contractId: string, name: string, etag: string): Promise<void> {
  await apiRequest<void>(`/api/contracts/${encodeURIComponent(contractId)}`, {
    method: 'PUT',
    headers: {
      'If-Match': etag
    },
    body: {
      name
    }
  });
}

export type ContractVersion = {
  id: string;
  versionNumber: number;
  status: string;
  createdAt: string;
  signedAt?: string;
  activatedAt?: string;
};

type RawContractVersion = {
  id: string;
  versionNumber: number;
  status: string | number;
  createdAt: string;
  signedAt?: string;
};

type RawContractHistoryResponse = {
  contract: ContractDetailsResponse;
  versions: RawContractVersion[];
};

export type ContractHistoryResponse = {
  contract: ContractDetailsResponse;
  versions: ContractVersion[];
};

function normalizeContractVersionStatus(status: string | number): string {
  if (typeof status === 'string') return status;
  if (status === 1) return 'Draft';
  if (status === 2) return 'Signed';
  if (status === 3) return 'Archived';
  return String(status);
}

export async function getContractHistory(contractId: string): Promise<ContractHistoryResponse> {
  const raw = await apiRequest<RawContractHistoryResponse>(`/api/contracts/${encodeURIComponent(contractId)}/history`, {
    method: 'GET'
  });

  return {
    contract: raw.contract,
    versions: (raw.versions ?? []).map((v) => ({
      id: v.id,
      versionNumber: v.versionNumber,
      status: normalizeContractVersionStatus(v.status),
      createdAt: v.createdAt,
      signedAt: v.signedAt
    }))
  };
}

export async function createContractVersion(contractId: string): Promise<void> {
  await apiRequest<void>(`/api/contracts/${encodeURIComponent(contractId)}/versions`, {
    method: 'POST',
    body: {
      effectiveFromUtc: new Date().toISOString()
    }
  });
}

export async function signContractVersion(contractId: string, versionId: string): Promise<void> {
  await apiRequest<void>(`/api/contracts/${encodeURIComponent(contractId)}/versions/${encodeURIComponent(versionId)}/sign`, {
    method: 'POST'
  });
}

export async function activateContractVersion(contractId: string, versionId: string): Promise<void> {
  await apiRequest<void>(`/api/contracts/${encodeURIComponent(contractId)}/versions/${encodeURIComponent(versionId)}/activate`, {
    method: 'POST'
  });
}

export type SlaRule = {
  id: string;
  metric: string;
  operator: string;
  threshold: number;
  penaltyAmount: number;
  conditionType?: 'threshold' | 'range' | 'boolean';
  minValue?: number | null;
  maxValue?: number | null;
  eventType?: string | null;
  scope?: unknown;
};

export type CreateSlaRulePayload = {
  metric: string;
  operator?: string;
  threshold?: number;
  penaltyAmount: number;
  conditionType?: 'threshold' | 'range' | 'boolean';
  minValue?: number | null;
  maxValue?: number | null;
  eventType?: string;
  scope?: unknown;
};

export async function getSlaRules(contractId: string, versionId: string): Promise<SlaRule[]> {
  return apiRequest<SlaRule[]>(
    `/api/contracts/${encodeURIComponent(contractId)}/versions/${encodeURIComponent(versionId)}/rules`,
    {
      method: 'GET'
    }
  );
}

export async function createSlaRule(
  contractId: string,
  versionId: string,
  rule: CreateSlaRulePayload
): Promise<void> {
  await apiRequest<void>(
    `/api/contracts/${encodeURIComponent(contractId)}/versions/${encodeURIComponent(versionId)}/rules`,
    {
      method: 'POST',
      body: rule
    }
  );
}

export async function deleteSlaRule(contractId: string, versionId: string, ruleId: string): Promise<void> {
  await apiRequest<void>(
    `/api/contracts/${encodeURIComponent(contractId)}/versions/${encodeURIComponent(versionId)}/rules/${encodeURIComponent(ruleId)}`,
    {
      method: 'DELETE'
    }
  );
}

export type FactImport = {
  id: string;
  fileName: string;
  rowsImported: number;
  claimsGenerated: number;
  createdAt: string;
};

export async function getFactImports(): Promise<FactImport[]> {
  return apiRequest<FactImport[]>('/api/imports', { method: 'GET' });
}

const FACT_IMPORT_TEMPLATE_FILE_NAME = 'fact_import_template.csv';
const FACT_IMPORT_TEMPLATE_CONTENT =
  'shipmentId;factType;eventType;cargoType;eventTime;value;counterpartyCode\n' +
  'SHP-001;DELIVERY_DELAY;DELIVERY_DELAY;ICE_CREAM;2026-01-01T10:00:00Z;45;CONTOSO\n' +
  'SHP-002;DOCUMENT_MISSING;DOCUMENT_MISSING;FROZEN_FISH;2026-01-01T11:00:00Z;1;NORTHWIND\n';

function triggerFileDownload(blob: Blob, fileName: string): void {
  const objectUrl = window.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.URL.revokeObjectURL(objectUrl);
}

export async function downloadFactImportTemplate(): Promise<void> {
  const baseUrl = getApiBaseUrl();
  const url = `${baseUrl}/api/imports/template`;
  const headers = new Headers();
  headers.set('Accept', 'text/csv');

  const token = getStoredToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const res = await fetch(url, {
    method: 'GET',
    headers
  });

  if (res.status === 401) {
    clearStoredToken();
    if (window.location.pathname !== '/login') window.location.href = '/login';
    throw new ApiError('Unauthorized', 401, null);
  }

  if (res.status === 404) {
    // Backward-compatible fallback when API route is unavailable in older backend builds.
    const fallbackBlob = new Blob([FACT_IMPORT_TEMPLATE_CONTENT], { type: 'text/csv;charset=utf-8' });
    triggerFileDownload(fallbackBlob, FACT_IMPORT_TEMPLATE_FILE_NAME);
    return;
  }

  if (!res.ok) {
    const payload = await res.text().catch(() => null);
    throw new ApiError(`Request failed (${res.status})`, res.status, payload);
  }

  const blob = await res.blob();
  triggerFileDownload(blob, FACT_IMPORT_TEMPLATE_FILE_NAME);
}

export type ImportFactsResponse = {
  imported: number;
  claimsGenerated: number;
};

export async function uploadFacts(file: File): Promise<ImportFactsResponse> {
  const baseUrl = getApiBaseUrl();
  const url = `${baseUrl}/api/imports/facts`;
  const headers = new Headers();
  headers.set('Accept', 'application/json');

  const token = getStoredToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const formData = new FormData();
  formData.append('file', file);

  const res = await fetch(url, {
    method: 'POST',
    headers,
    body: formData
  });

  if (res.status === 401) {
    clearStoredToken();
    if (window.location.pathname !== '/login') window.location.href = '/login';
    throw new ApiError('Unauthorized', 401, null);
  }

  const contentType = res.headers.get('content-type') ?? '';
  const isJson = contentType.includes('application/json');
  const payload = isJson ? await res.json().catch(() => null) : await res.text().catch(() => null);

  if (!res.ok) {
    throw new ApiError(`Request failed (${res.status})`, res.status, payload);
  }

  return payload as ImportFactsResponse;
}

export type Claim = {
  id: string;
  shipmentId: string | null;
  ruleId: string;
  metric?: string;
  conditionType?: 'threshold' | 'range' | 'boolean';
  operator?: string;
  threshold?: number | null;
  minValue?: number | null;
  maxValue?: number | null;
  eventType?: string | null;
  scope?: unknown;
  calculatedValues?: Record<string, unknown> | null;
  penaltyAmount: number;
  currency?: string;
  createdAt: string;
};

export async function getClaims(): Promise<Claim[]> {
  return apiRequest<Claim[]>('/api/claims', { method: 'GET' });
}

export type ClaimsSummary = {
  totalClaims: number;
  totalPenalty: number;
  byRule: Array<{
    ruleId: string;
    count: number;
    penalty: number;
  }>;
};

export async function getClaimsSummary(): Promise<ClaimsSummary> {
  return apiRequest<ClaimsSummary>('/api/claims/summary', { method: 'GET' });
}
