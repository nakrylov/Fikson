import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError, Claim, ClaimsSummary, getClaims, getClaimsSummary } from '../api/api';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { t } from '../i18n';

export function ClaimsPage() {
  const [items, setItems] = useState<Claim[]>([]);
  const [summary, setSummary] = useState<ClaimsSummary | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});

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

  const extractCargoTypeScope = useCallback((scope: unknown): string | undefined => {
    if (!scope || typeof scope !== 'object' || Array.isArray(scope)) return undefined;
    const value = (scope as Record<string, unknown>).cargoType;
    return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
  }, []);

  const buildRuleDescription = useCallback((claim: Claim) => {
    const metric = claim.metric ?? claim.ruleId;
    const conditionType = claim.conditionType ?? 'threshold';
    let description = '';

    if (conditionType === 'range') {
      description = `${t.rules.if} ${metric} NOT IN [${claim.minValue ?? ''} ... ${claim.maxValue ?? ''}]`;
    } else if (conditionType === 'boolean') {
      description = `${t.rules.if} ${claim.eventType ?? 'DOCUMENT_MISSING'} occurred`;
    } else {
      description = `${t.rules.if} ${metric} ${claim.operator ?? '>'} ${claim.threshold ?? ''}`;
    }

    const cargoType = extractCargoTypeScope(claim.scope);
    if (cargoType) {
      description += ` ${t.rules.and} cargoType = ${cargoType}`;
    }

    description += ` -> ${t.rules.penalty} ${claim.penaltyAmount} ${claim.currency ?? 'EUR'}`;
    return description;
  }, [extractCargoTypeScope]);

  const detailsEntries = useCallback((claim: Claim): Array<{ key: string; value: string }> => {
    const result: Array<{ key: string; value: string }> = [];
    const calculated = claim.calculatedValues;
    if (calculated && typeof calculated === 'object' && !Array.isArray(calculated)) {
      Object.entries(calculated).forEach(([key, value]) => {
        if (key === 'shipmentId') return;
        result.push({ key, value: String(value) });
      });
    }

    if (claim.conditionType === 'range') {
      result.push({
        key: 'expected range',
        value: `[${claim.minValue ?? ''} ... ${claim.maxValue ?? ''}]`
      });
    }

    return result;
  }, []);

  const toggleDetails = useCallback((claimId: string) => {
    setExpanded((prev) => ({ ...prev, [claimId]: !prev[claimId] }));
  }, []);

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
        {!loading && !error ? (
          items.length === 0 ? (
            <div>{t.claims.empty}</div>
          ) : (
            <div className="space-y-4">
              {items.map((claim) => {
                const details = detailsEntries(claim);
                const isExpanded = Boolean(expanded[claim.id]);
                return (
                  <Card key={claim.id}>
                    <div className="space-y-2 text-sm">
                      <div className="font-medium">
                        {t.claims.shipment}: {claim.shipmentId ?? t.common.unknown}
                      </div>
                      <div>{buildRuleDescription(claim)}</div>
                      <div>
                        <Button type="button" variant="secondary" onClick={() => toggleDetails(claim.id)}>
                          {isExpanded ? t.claims.hideDetails : t.claims.showDetails}
                        </Button>
                      </div>
                      {isExpanded && details.length > 0 ? (
                        <div className="bg-gray-50 p-2 rounded space-y-1">
                          {details.map((entry) => (
                            <div key={entry.key}>
                              {entry.key}: {entry.value}
                            </div>
                          ))}
                        </div>
                      ) : null}
                      <div className="font-medium">
                        {t.claims.penalty}: {claim.penaltyAmount} {claim.currency ?? 'EUR'}
                      </div>
                    </div>
                  </Card>
                );
              })}
            </div>
          )
        ) : null}
      </Card>
    </div>
  );
}
