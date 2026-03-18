import React, { useCallback, useEffect, useState } from 'react';
import { ApiError, Claim, ClaimsSummary, getClaims, getClaimsSummary } from '../api/api';
import { t } from '../i18n';

export function ClaimsPage() {
  const [items, setItems] = useState<Claim[]>([]);
  const [summary, setSummary] = useState<ClaimsSummary | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const loadClaims = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [claimsData, summaryData] = await Promise.all([getClaims(), getClaimsSummary()]);
      setItems(claimsData);
      setSummary(summaryData);
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status >= 500) {
        setError(t.common.serverError);
      } else {
        setError(t.common.requestFailed);
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadClaims();
  }, [loadClaims]);

  return (
    <div>
      <h2>{t.claims.title}</h2>

      {summary ? (
        <div style={{ marginBottom: 12 }}>
          <h3>{t.claims.summaryTitle}</h3>
          <div>
            {t.claims.totalClaims}: {summary.totalClaims}
          </div>
          <div>
            {t.claims.totalPenalty}: {summary.totalPenalty}
          </div>
        </div>
      ) : null}

      {loading ? <div>{t.common.loading}</div> : null}
      {error ? <div style={{ color: 'red' }}>{error}</div> : null}

      {!loading && !error && items.length === 0 ? <div>{t.claims.empty}</div> : null}

      {!loading && !error && items.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>{t.claims.shipment}</th>
              <th>{t.claims.rule}</th>
              <th>{t.claims.penalty}</th>
              <th>{t.claims.created}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((claim) => (
              <tr key={claim.id}>
                <td>{claim.shipmentId ?? t.common.unknown}</td>
                <td>{claim.ruleId}</td>
                <td>{claim.penaltyAmount}</td>
                <td>{claim.createdAt}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
