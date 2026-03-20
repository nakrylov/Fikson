import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError, ClaimsSummary, FactImport, getClaimsSummary, getFactImports } from '../api/api';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { t } from '../i18n';

export function DashboardPage() {
  const navigate = useNavigate();
  const [summary, setSummary] = useState<ClaimsSummary | null>(null);
  const [imports, setImports] = useState<FactImport[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [summaryData, importsData] = await Promise.all([getClaimsSummary(), getFactImports()]);
      setSummary(summaryData);
      setImports(importsData);
    } catch (e: unknown) {
      if (e instanceof ApiError) {
        if (e.status === 403) setError(t.common.forbidden);
        else if (e.status >= 500) setError(t.common.serverError);
        else setError(t.common.requestFailed);
      } else {
        setError(t.common.requestFailed);
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const recentImports = imports.slice(0, 5);

  return (
    <div className="space-y-6">
      <Card title={t.dashboard.title}>
        {loading ? <div>{t.common.loading}</div> : null}
        {error ? <div style={{ color: 'red' }}>{error}</div> : null}
        {!loading && !error ? (
          <div className="space-y-2">
            <div className="text-sm text-gray-500">{t.dashboard.totalClaims}</div>
            <div className="text-3xl font-semibold">{summary?.totalClaims ?? 0}</div>
            <div className="text-sm text-gray-500">{t.dashboard.totalPenalty}</div>
            <div className="text-3xl font-semibold">{summary?.totalPenalty ?? 0}</div>
          </div>
        ) : null}
      </Card>

      <Card title={t.dashboard.recentImports}>
        {recentImports.length === 0 ? (
          <div>{t.imports.empty}</div>
        ) : (
          <table className="table-base">
            <thead>
              <tr>
                <th>{t.imports.file}</th>
                <th>{t.imports.rows}</th>
                <th>{t.imports.claims}</th>
                <th>{t.imports.date}</th>
              </tr>
            </thead>
            <tbody>
              {recentImports.map((item) => (
                <tr key={item.id}>
                  <td>{item.fileName}</td>
                  <td>{item.rowsImported}</td>
                  <td>{item.claimsGenerated}</td>
                  <td>{new Date(item.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      <Card title={t.dashboard.quickActions}>
        <div className="flex gap-2 flex-wrap">
          <Button type="button" variant="primary" onClick={() => navigate('/contracts')}>
            {t.nav.contracts}
          </Button>
          <Button type="button" variant="secondary" onClick={() => navigate('/imports')}>
            {t.nav.imports}
          </Button>
          <Button type="button" variant="secondary" onClick={() => navigate('/claims')}>
            {t.nav.claims}
          </Button>
        </div>
      </Card>
    </div>
  );
}
