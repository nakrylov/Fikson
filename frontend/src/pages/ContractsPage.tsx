import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError, apiRequest } from '../api/api';
import { useAuth } from '../hooks/useAuth';

/**
 * /contracts
 * Contracts list + minimal create form (no UI libs).
 */

export interface Contract {
  id: string;
  name: string;
  counterpartyId: string;
  status?: string;
};

type ContractsListResponse = {
  tenantId: string;
  items: Array<{
    id: string;
    name: string;
    counterpartyId: string;
    status?: string | number;
  }>;
};

export function ContractsPage() {
  const { tenantId, role, logout } = useAuth();
  const navigate = useNavigate();

  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [createName, setCreateName] = useState<string>('');
  const [createCounterpartyId, setCreateCounterpartyId] = useState<string>('');
  const [createLoading, setCreateLoading] = useState<boolean>(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const mapToContract = useCallback((x: ContractsListResponse['items'][number]): Contract => {
    return {
      id: x.id,
      name: x.name,
      counterpartyId: x.counterpartyId,
      status: x.status !== undefined && x.status !== null ? String(x.status) : undefined
    };
  }, []);

  const loadContracts = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const data = await apiRequest<ContractsListResponse>('/api/contracts');
      setContracts(data.items.map(mapToContract));
    } catch (e: unknown) {
      if (e instanceof ApiError) {
        if (e.status === 403) setLoadError('Forbidden');
        else if (e.status >= 500) setLoadError('Server error');
        else setLoadError('Request failed');
      } else {
        setLoadError('Request failed');
      }
    } finally {
      setLoading(false);
    }
  }, [mapToContract]);

  useEffect(() => {
    void loadContracts();
  }, [loadContracts]);

  const onCreateSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setCreateError(null);
      setCreateLoading(true);
      try {
        const idempotencyKey = crypto.randomUUID();

        await apiRequest('/api/contracts', {
          method: 'POST',
          headers: {
            'Idempotency-Key': idempotencyKey
          },
          body: {
            name: createName,
            counterpartyId: createCounterpartyId
          }
        });

        // Refresh list and reset form on success.
        await loadContracts();
        setCreateName('');
        setCreateCounterpartyId('');
        setShowCreate(false);
      } catch (e2: unknown) {
        if (e2 instanceof ApiError) {
          if (e2.status === 400) setCreateError('Validation error');
          else if (e2.status === 403) setCreateError('Forbidden');
          else if (e2.status >= 500) setCreateError('Server error');
          else setCreateError('Request failed');
        } else {
          setCreateError('Request failed');
        }
      } finally {
        setCreateLoading(false);
      }
    },
    [createName, createCounterpartyId, loadContracts]
  );

  const tableRows = useMemo(() => contracts, [contracts]);

  return (
    <div>
      <h2>Contracts</h2>

      <div>
        <div>tenantId (from JWT): {tenantId ?? '(unknown)'}</div>
        <div>role (from JWT): {role ?? '(unknown)'}</div>
        <button type="button" onClick={() => logout()}>
          Logout
        </button>
        <button type="button" onClick={() => void loadContracts()} disabled={loading}>
          Refresh
        </button>
        <button type="button" onClick={() => setShowCreate((v) => !v)}>
          Create Contract
        </button>
      </div>

      {loading ? <div>Loading…</div> : null}
      {loadError ? <div style={{ color: 'red' }}>{loadError}</div> : null}

      {showCreate ? (
        <form onSubmit={onCreateSubmit} style={{ marginTop: 12, marginBottom: 12 }}>
          <div>
            <label style={{ display: 'block' }}>
              <div>Name</div>
              <input
                value={createName}
                onChange={(ev) => setCreateName(ev.target.value)}
                disabled={createLoading}
              />
            </label>
          </div>
          <div>
            <label style={{ display: 'block' }}>
              <div>CounterpartyId</div>
              <input
                value={createCounterpartyId}
                onChange={(ev) => setCreateCounterpartyId(ev.target.value)}
                disabled={createLoading}
                placeholder="GUID"
              />
            </label>
          </div>

          <button type="submit" disabled={createLoading}>
            {createLoading ? 'Creating…' : 'Submit'}
          </button>
          <button type="button" onClick={() => setShowCreate(false)} disabled={createLoading}>
            Cancel
          </button>

          {createError ? <div style={{ color: 'red' }}>{createError}</div> : null}
        </form>
      ) : null}

      <table>
        <thead>
          <tr>
            <th>id</th>
            <th>name</th>
            <th>counterpartyId</th>
            <th>status</th>
            <th>actions</th>
          </tr>
        </thead>
        <tbody>
          {tableRows.map((c) => (
            <tr key={c.id}>
              <td>{c.id}</td>
              <td>{c.name}</td>
              <td>{c.counterpartyId}</td>
              <td>{c.status ?? ''}</td>
              <td>
                <button type="button" onClick={() => navigate(`/contract/${c.id}`)}>
                  View
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

