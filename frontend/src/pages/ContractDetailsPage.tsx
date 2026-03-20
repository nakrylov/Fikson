import React from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  activateContractVersion,
  ApiError,
  ContractDashboard,
  ContractDetailsResponse,
  ContractVersion,
  createSlaRule,
  createContractVersion,
  deleteSlaRule,
  getContractDashboard,
  getContractDetailsWithEtag,
  getContractHistory,
  getSlaRules,
  SlaRule,
  signContractVersion,
  updateContractName
} from '../api/api';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { FormField } from '../components/FormField';
import { Input } from '../components/Input';
import { metrics } from '../constants/metrics';
import { t } from '../i18n';

/**
 * /contract/:id
 * Minimal contract details page.
 */

export function ContractDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const [data, setData] = React.useState<ContractDetailsResponse | null>(null);
  const [etag, setEtag] = React.useState<string | null>(null);
  const [loading, setLoading] = React.useState<boolean>(false);
  const [error, setError] = React.useState<string | null>(null);
  const [isEditing, setIsEditing] = React.useState<boolean>(false);
  const [nameDraft, setNameDraft] = React.useState<string>('');
  const [saveLoading, setSaveLoading] = React.useState<boolean>(false);
  const [saveError, setSaveError] = React.useState<string | null>(null);
  const [versions, setVersions] = React.useState<ContractVersion[]>([]);
  const [currentVersionId, setCurrentVersionId] = React.useState<string | null>(null);
  const [loadingVersions, setLoadingVersions] = React.useState<boolean>(false);
  const [errorVersions, setErrorVersions] = React.useState<string | null>(null);
  const [versionActionLoading, setVersionActionLoading] = React.useState<boolean>(false);
  const [rules, setRules] = React.useState<SlaRule[]>([]);
  const [loadingRules, setLoadingRules] = React.useState<boolean>(false);
  const [errorRules, setErrorRules] = React.useState<string | null>(null);
  const [ruleActionLoading, setRuleActionLoading] = React.useState<boolean>(false);
  const [dashboard, setDashboard] = React.useState<ContractDashboard | null>(null);
  const [loadingDashboard, setLoadingDashboard] = React.useState<boolean>(false);
  const [errorDashboard, setErrorDashboard] = React.useState<string | null>(null);
  const [metricDraft, setMetricDraft] = React.useState<string>('DELIVERY_DELAY');
  const [operatorDraft, setOperatorDraft] = React.useState<string>('>');
  const [thresholdDraft, setThresholdDraft] = React.useState<string>('0');
  const [penaltyDraft, setPenaltyDraft] = React.useState<string>('0');
  const thresholdValue = Number(thresholdDraft);
  const penaltyValue = Number(penaltyDraft);
  const selectedMetric = React.useMemo(
    () => metrics.find((metric) => metric.code === metricDraft) ?? metrics[0],
    [metricDraft]
  );
  const isBooleanMetric = selectedMetric.type === 'boolean';
  const canCreateRule =
    Number.isFinite(penaltyValue) &&
    penaltyValue > 0 &&
    (isBooleanMetric || (Number.isFinite(thresholdValue) && thresholdValue > 0));
  const ruleThresholdFieldError =
    !isBooleanMetric && errorRules && errorRules.includes(t.rules.threshold) ? errorRules : undefined;
  const rulePenaltyFieldError = errorRules && errorRules.includes(t.rules.penalty) ? errorRules : undefined;
  const getMetricLabel = React.useCallback((metricCode: string) => {
    if (metricCode === 'DELIVERY_DELAY') return t.rules.metrics.deliveryDelay;
    if (metricCode === 'TEMPERATURE') return t.rules.metrics.temperature;
    if (metricCode === 'MISSING_DOCS') return t.rules.metrics.missingDocs;
    return metricCode;
  }, []);

  const loadContract = React.useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const result = await getContractDetailsWithEtag(id);
      setData(result.contract);
      setEtag(result.etag);
      setNameDraft(result.contract.name);
    } catch {
      setError(t.errors.failedToLoadContract);
    } finally {
      setLoading(false);
    }
  }, [id]);

  React.useEffect(() => {
    void loadContract();
  }, [loadContract]);

  const loadVersions = React.useCallback(async () => {
    if (!id) return;
    setLoadingVersions(true);
    setErrorVersions(null);
    try {
      const history = await getContractHistory(id);
      setVersions(history.versions ?? []);
      setCurrentVersionId(history.contract.currentVersionId ?? null);
    } catch {
      setErrorVersions(t.common.requestFailed);
    } finally {
      setLoadingVersions(false);
    }
  }, [id]);

  React.useEffect(() => {
    void loadVersions();
  }, [loadVersions]);

  const activatedVersion = React.useMemo(
    () => versions.find((v) => currentVersionId !== null && v.id === currentVersionId) ?? null,
    [versions, currentVersionId]
  );
  const draftVersion = React.useMemo(() => versions.find((v) => v.status === 'Draft') ?? null, [versions]);
  const signedVersion = React.useMemo(() => versions.find((v) => v.status === 'Signed') ?? null, [versions]);

  const loadRules = React.useCallback(async () => {
    if (!id || !activatedVersion) {
      setRules([]);
      return;
    }

    setLoadingRules(true);
    setErrorRules(null);
    try {
      const dataRules = await getSlaRules(id, activatedVersion.id);
      setRules(dataRules);
    } catch {
      setErrorRules(t.common.requestFailed);
    } finally {
      setLoadingRules(false);
    }
  }, [id, activatedVersion]);

  React.useEffect(() => {
    void loadRules();
  }, [loadRules]);

  const loadDashboard = React.useCallback(async () => {
    if (!id) return;
    setLoadingDashboard(true);
    setErrorDashboard(null);
    try {
      const dataDashboard = await getContractDashboard(id);
      setDashboard(dataDashboard);
    } catch {
      setErrorDashboard(t.common.requestFailed);
    } finally {
      setLoadingDashboard(false);
    }
  }, [id]);

  React.useEffect(() => {
    void loadDashboard();
  }, [loadDashboard]);

  const onStartEdit = React.useCallback(() => {
    setIsEditing(true);
    setSaveError(null);
    setNameDraft(data?.name ?? '');
  }, [data]);

  const onCancelEdit = React.useCallback(() => {
    setIsEditing(false);
    setSaveError(null);
    setNameDraft(data?.name ?? '');
  }, [data]);

  const onSave = React.useCallback(async () => {
    if (!id || !etag) {
      setSaveError(t.common.requestFailed);
      return;
    }

    setSaveLoading(true);
    setSaveError(null);
    try {
      await updateContractName(id, nameDraft, etag);
      await loadContract();
      setIsEditing(false);
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 412) {
        setSaveError(t.contracts.conflictError);
      } else if (err instanceof ApiError && err.status >= 500) {
        setSaveError(t.common.serverError);
      } else {
        setSaveError(t.common.requestFailed);
      }
    } finally {
      setSaveLoading(false);
    }
  }, [id, etag, nameDraft, loadContract]);

  const onCreateVersion = React.useCallback(async () => {
    if (!id) return;
    setVersionActionLoading(true);
    setErrorVersions(null);
    try {
      await createContractVersion(id);
      await loadVersions();
    } catch {
      setErrorVersions(t.common.requestFailed);
    } finally {
      setVersionActionLoading(false);
    }
  }, [id, loadVersions]);

  const onSignVersion = React.useCallback(
    async (versionId: string) => {
      if (!id) return;
      setVersionActionLoading(true);
      setErrorVersions(null);
      try {
        await signContractVersion(id, versionId);
        await loadVersions();
      } catch {
        setErrorVersions(t.common.requestFailed);
      } finally {
        setVersionActionLoading(false);
      }
    },
    [id, loadVersions]
  );

  const onActivateVersion = React.useCallback(
    async (versionId: string) => {
      if (!id) return;
      setVersionActionLoading(true);
      setErrorVersions(null);
      try {
        await activateContractVersion(id, versionId);
        await loadVersions();
      } catch {
        setErrorVersions(t.common.requestFailed);
      } finally {
        setVersionActionLoading(false);
      }
    },
    [id, loadVersions]
  );

  const onCreateRule = React.useCallback(async () => {
    if (!id || !activatedVersion) return;
    if (!canCreateRule) {
      setErrorRules(
        isBooleanMetric
          ? `${t.common.validationError}: ${t.rules.penalty} > 0`
          : `${t.common.validationError}: ${t.rules.threshold} > 0, ${t.rules.penalty} > 0`
      );
      return;
    }

    setRuleActionLoading(true);
    setErrorRules(null);
    try {
      await createSlaRule(id, activatedVersion.id, {
        metric: selectedMetric.code,
        operator: isBooleanMetric ? 'EQUALS' : operatorDraft,
        threshold: isBooleanMetric ? 1 : thresholdValue,
        penaltyAmount: penaltyValue
      });
      await loadRules();
      setMetricDraft('DELIVERY_DELAY');
      setOperatorDraft('>');
      setThresholdDraft('');
      setPenaltyDraft('');
    } catch {
      setErrorRules(t.common.requestFailed);
    } finally {
      setRuleActionLoading(false);
    }
  }, [id, activatedVersion, canCreateRule, isBooleanMetric, selectedMetric, operatorDraft, thresholdValue, penaltyValue, loadRules]);

  const onDeleteRule = React.useCallback(
    async (ruleId: string) => {
      if (!id || !activatedVersion) return;
      setRuleActionLoading(true);
      setErrorRules(null);
      try {
        await deleteSlaRule(id, activatedVersion.id, ruleId);
        await loadRules();
      } catch {
        setErrorRules(t.common.requestFailed);
      } finally {
        setRuleActionLoading(false);
      }
    },
    [id, activatedVersion, loadRules]
  );

  return (
    <div className="space-y-6">
      <Card
        title={t.contracts.detailsTitle}
        actions={(
          <div className="flex gap-2 flex-wrap">
            <Link to="/contracts" className="px-4 py-2 rounded-md transition bg-white text-gray-700 border border-gray-300 hover:bg-gray-50">
              {t.common.backToList}
            </Link>
            <Button type="button" variant="secondary" onClick={() => void loadContract()}>
              {t.common.refresh}
            </Button>
          </div>
        )}
      >
        {loading ? <div>{t.common.loading}</div> : null}
        {error ? <div style={{ color: 'red' }}>{error}</div> : null}
        {data ? (
          !isEditing ? (
            <div className="space-y-2">
              <div>
                {t.contracts.name}: {data.name}
              </div>
              <Button type="button" variant="secondary" onClick={onStartEdit}>
                {t.contracts.edit}
              </Button>
            </div>
          ) : (
            <div className="space-y-4">
              <FormField label={t.contracts.name} error={saveError ?? undefined}>
                <Input
                  value={nameDraft}
                  onChange={(ev) => setNameDraft(ev.target.value)}
                  disabled={saveLoading}
                  error={Boolean(saveError)}
                />
              </FormField>
              <div className="flex gap-2">
                <Button type="button" variant="primary" onClick={() => void onSave()} disabled={saveLoading}>
                  {t.contracts.save}
                </Button>
                <Button type="button" variant="secondary" onClick={onCancelEdit} disabled={saveLoading}>
                  {t.contracts.cancel}
                </Button>
              </div>
            </div>
          )
        ) : null}
      </Card>

      <Card
        title={t.contracts.versions}
        actions={(
          <Button type="button" variant="primary" onClick={() => void onCreateVersion()} disabled={versionActionLoading}>
            {t.contracts.createVersion}
          </Button>
        )}
      >
        {loadingVersions ? <div>{t.common.loading}</div> : null}
        {errorVersions ? <div style={{ color: 'red' }}>{errorVersions}</div> : null}

        {versions.length > 0 ? (
          <table className="table-base">
            <thead>
              <tr>
                <th>{t.contracts.versionNumber}</th>
                <th>{t.contracts.status}</th>
                <th>{t.contracts.createdAt}</th>
                <th>{t.contracts.actions}</th>
              </tr>
            </thead>
            <tbody>
              {versions.map((v) => (
                <tr key={v.id}>
                  <td>{v.versionNumber}</td>
                  <td>{v.status}</td>
                  <td>{v.createdAt}</td>
                  <td className="flex gap-2">
                    {v.status === 'Draft' ? (
                      <Button
                        type="button"
                        variant="primary"
                        onClick={() => void onSignVersion(v.id)}
                        disabled={versionActionLoading}
                      >
                        {t.contracts.signVersion}
                      </Button>
                    ) : null}
                    {v.status === 'Signed' ? (
                      <Button
                        type="button"
                        variant="primary"
                        onClick={() => void onActivateVersion(v.id)}
                        disabled={versionActionLoading}
                      >
                        {t.contracts.activateVersion}
                      </Button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </Card>

      <Card title={t.dashboard.contractTitle}>
        {loadingDashboard ? <div>{t.common.loading}</div> : null}
        {errorDashboard ? <div style={{ color: 'red' }}>{errorDashboard}</div> : null}
        {dashboard ? (
          <div className="space-y-1">
            <div>
              {t.dashboard.totalPenalty}: {dashboard.totalPenalty}
            </div>
            <div>
              {t.dashboard.totalClaims}: {dashboard.totalClaims}
            </div>
            <div>
              {t.dashboard.shipments}: {dashboard.shipmentsAffected}
            </div>
            <div>
              {t.dashboard.lastImport}:{' '}
              {dashboard.lastImportDate ? new Date(dashboard.lastImportDate).toLocaleString() : t.common.noData}
            </div>
          </div>
        ) : null}
      </Card>

      <Card title={t.rules.title}>
        {!activatedVersion ? (
          <div className="bg-yellow-50 border border-yellow-200 p-4 rounded">
            <div>{t.contracts.noActiveVersion}</div>
            <ol>
              <li>1. {t.contracts.createVersionHint}</li>
              <li>2. {t.contracts.signVersionHint}</li>
              <li>3. {t.contracts.activateVersionHint}</li>
            </ol>
            <div className="flex gap-2 flex-wrap mt-2">
              <Button type="button" variant="primary" onClick={() => void onCreateVersion()} disabled={versionActionLoading}>
                {t.contracts.createVersion}
              </Button>
              {draftVersion ? (
                <Button
                  type="button"
                  variant="primary"
                  onClick={() => void onSignVersion(draftVersion.id)}
                  disabled={versionActionLoading}
                >
                  {t.contracts.signVersion}
                </Button>
              ) : null}
              {signedVersion ? (
                <Button
                  type="button"
                  variant="primary"
                  onClick={() => void onActivateVersion(signedVersion.id)}
                  disabled={versionActionLoading}
                >
                  {t.contracts.activateVersion}
                </Button>
              ) : null}
            </div>
          </div>
        ) : (
          <>
            <div className="space-y-4">
              <FormField label={t.rules.metric}>
                <select
                  value={metricDraft}
                  onChange={(ev) => setMetricDraft(ev.target.value)}
                  disabled={ruleActionLoading}
                >
                  {metrics.map((metric) => (
                    <option key={metric.code} value={metric.code}>
                      {getMetricLabel(metric.code)}
                    </option>
                  ))}
                </select>
              </FormField>
              {!isBooleanMetric ? (
                <>
                  <FormField label={t.rules.operator}>
                    <select
                      value={operatorDraft}
                      onChange={(ev) => setOperatorDraft(ev.target.value)}
                      disabled={ruleActionLoading}
                    >
                      <option value=">">{'>'}</option>
                      <option value=">=">{'>='}</option>
                      <option value="<">{'<'}</option>
                    </select>
                  </FormField>
                  <FormField label={t.rules.threshold} error={ruleThresholdFieldError}>
                    <div className="flex items-center gap-2">
                      <Input
                        type="number"
                        min={1}
                        value={thresholdDraft}
                        onChange={(ev) => setThresholdDraft(ev.target.value)}
                        disabled={ruleActionLoading}
                        className="w-24"
                        error={Boolean(ruleThresholdFieldError)}
                      />
                      <span>{selectedMetric.unit}</span>
                    </div>
                  </FormField>
                </>
              ) : (
                <FormField label={t.rules.operator}>
                  <div className="flex items-center gap-2">
                    <span>{t.rules.booleanCondition}</span>
                  </div>
                </FormField>
              )}
              <FormField label={t.rules.penalty} error={rulePenaltyFieldError}>
                <div className="flex items-center gap-2">
                  <Input
                    type="number"
                    min={1}
                    value={penaltyDraft}
                    onChange={(ev) => setPenaltyDraft(ev.target.value)}
                    disabled={ruleActionLoading}
                    className="w-24"
                    error={Boolean(rulePenaltyFieldError)}
                  />
                  <span>EUR</span>
                </div>
              </FormField>
              <div className="bg-gray-50 p-3 rounded border">
                {t.rules.preview}:{' '}
                {isBooleanMetric ? (
                  <>IF {getMetricLabel(selectedMetric.code)} -&gt; penalty {penaltyDraft || '0'} €</>
                ) : (
                  <>IF {getMetricLabel(selectedMetric.code)} {operatorDraft} {thresholdDraft || '0'} {selectedMetric.unit} -&gt; penalty {penaltyDraft || '0'} €</>
                )}
              </div>
              <Button
                type="button"
                variant="primary"
                onClick={() => void onCreateRule()}
                disabled={ruleActionLoading || !canCreateRule}
              >
                {t.rules.create}
              </Button>
            </div>

            {loadingRules ? <div>{t.common.loading}</div> : null}
            {errorRules && !rulePenaltyFieldError && !ruleThresholdFieldError ? (
              <div style={{ color: 'red' }}>{errorRules}</div>
            ) : null}

            {!loadingRules && !errorRules && rules.length === 0 ? <div>{t.rules.empty}</div> : null}

            {rules.length > 0 ? (
              <table className="table-base">
                <thead>
                  <tr>
                    <th>{t.rules.metric}</th>
                    <th>{t.rules.operator}</th>
                    <th>{t.rules.threshold}</th>
                    <th>{t.rules.penalty}</th>
                    <th>{t.contracts.actions}</th>
                  </tr>
                </thead>
                <tbody>
                  {rules.map((rule) => (
                    <tr key={rule.id}>
                      <td>{rule.metric}</td>
                      <td>{rule.operator}</td>
                      <td>{rule.threshold}</td>
                      <td>{rule.penaltyAmount}</td>
                      <td>
                        <Button
                          type="button"
                          variant="danger"
                          onClick={() => void onDeleteRule(rule.id)}
                          disabled={ruleActionLoading}
                        >
                          {t.rules.delete}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : null}
          </>
        )}
      </Card>

      <Card title="Debug">
        <pre style={{ whiteSpace: 'pre-wrap' }}>{data ? JSON.stringify(data, null, 2) : t.common.noData}</pre>
      </Card>
    </div>
  );
}

