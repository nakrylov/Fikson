import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError, Claim, ClaimsSummary, getClaims, getClaimsSummary } from '../api/api';
import { Card } from '../components/Card';
import { Column, Table } from '../components/Table';
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

  const claimColumns = useMemo<Column<Claim>[]>(() => [
    {
      key: 'shipmentId',
      title: t.claims.shipment,
      render: (claim) => claim.shipmentId ?? t.common.unknown
    },
    { key: 'ruleId', title: t.claims.rule },
    { key: 'penaltyAmount', title: t.claims.penalty },
    { key: 'createdAt', title: t.claims.created }
  ], []);

  return (
    <div className="space-y-6">
      <Card title={t.claims.summaryTitle}>
        {loading ? <div>{t.common.loading}</div> : null}
        {error ? <div style={{ color: 'red' }}>{error}</div> : null}
        {summary ? (
          <div className="space-y-1">
            <div>
              {t.claims.totalClaims}: {summary.totalClaims}
            </div>
            <div>
              {t.claims.totalPenalty}: {summary.totalPenalty}
            </div>
          </div>
        ) : null}
      </Card>

      <Card title={t.claims.title}>
        {!loading && !error ? <Table columns={claimColumns} data={items} emptyText={t.claims.empty} /> : null}
      </Card>
    </div>
  );
}
