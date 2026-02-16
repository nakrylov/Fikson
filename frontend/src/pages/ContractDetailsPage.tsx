import React from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiRequest } from '../api/api';
import { useFetch } from '../hooks/useFetch';

/**
 * /contract/:id
 * Minimal contract details page.
 */

type ContractDetails = {
  id: string;
  name: string;
  counterpartyId: string;
  status: number | string;
  createdAt: string;
  currentVersionId?: string | null;
};

export function ContractDetailsPage() {
  const { id } = useParams<{ id: string }>();

  const { data, loading, error, refetch } = useFetch<ContractDetails>(
    () => apiRequest<ContractDetails>(`/api/contracts/${id}`),
    [id]
  );

  return (
    <div>
      <h2>Contract details</h2>
      <div>
        <Link to="/contracts">Back to list</Link>
        <button type="button" onClick={() => refetch()}>
          Refresh
        </button>
      </div>

      {loading ? <div>Loading…</div> : null}
      {error ? <div style={{ color: 'red' }}>Failed to load contract.</div> : null}

      <pre style={{ whiteSpace: 'pre-wrap' }}>{data ? JSON.stringify(data, null, 2) : 'No data'}</pre>
    </div>
  );
}

