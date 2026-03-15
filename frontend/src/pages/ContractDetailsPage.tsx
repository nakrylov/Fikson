import React from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiRequest } from '../api/api';
import { useFetch } from '../hooks/useFetch';
import { t } from '../i18n';

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
      <h2>{t.contracts.detailsTitle}</h2>
      <div>
        <Link to="/contracts">{t.common.backToList}</Link>
        <button type="button" onClick={() => refetch()}>
          {t.common.refresh}
        </button>
      </div>

      {loading ? <div>{t.common.loading}</div> : null}
      {error ? <div style={{ color: 'red' }}>{t.errors.failedToLoadContract}</div> : null}

      <pre style={{ whiteSpace: 'pre-wrap' }}>{data ? JSON.stringify(data, null, 2) : t.common.noData}</pre>
    </div>
  );
}

