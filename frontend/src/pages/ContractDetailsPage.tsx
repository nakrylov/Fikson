import React from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  activateContractVersion,
  ApiError,
  ContractDashboard,
  ContractDetailsResponse,
  ContractVersion,
  Counterparty,
  createSlaRule,
  createContractVersion,
  deleteSlaRule,
  FactTypeDefinition,
  getContractDashboard,
  getContractDetailsWithEtag,
  getContractHistory,
  getCounterparties,
  getSlaRules,
  SlaRule,
  signContractVersion,
  updateContract
} from '../api/api';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { FormField } from '../components/FormField';
import { Input } from '../components/Input';
import { metrics } from '../constants/metrics';
import { ruleTemplates } from '../constants/ruleTemplates';
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
  const [counterpartyDraft, setCounterpartyDraft] = React.useState<string>('');
  const [counterparties, setCounterparties] = React.useState<Counterparty[]>([]);
  const [loadingCounterparties, setLoadingCounterparties] = React.useState<boolean>(false);
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
  const [factTypes, setFactTypes] = React.useState<FactTypeDefinition[]>([]);
  const [metricDraft, setMetricDraft] = React.useState<string>('DELIVERY_DELAY');
  const [templateDraft, setTemplateDraft] = React.useState<string>('');
  const [conditionTypeDraft, setConditionTypeDraft] = React.useState<'threshold' | 'range' | 'boolean'>('threshold');
  const [operatorDraft, setOperatorDraft] = React.useState<string>('>');
  const [thresholdDraft, setThresholdDraft] = React.useState<string>('0');
  const [minValueDraft, setMinValueDraft] = React.useState<string>('');
  const [maxValueDraft, setMaxValueDraft] = React.useState<string>('');
  const [eventTypeDraft, setEventTypeDraft] = React.useState<string>('DOCUMENT_MISSING');
  const [cargoTypeScopeDraft, setCargoTypeScopeDraft] = React.useState<string>('');
  const [penaltyDraft, setPenaltyDraft] = React.useState<string>('0');
  const thresholdValue = Number(thresholdDraft);
  const minValue = Number(minValueDraft);
  const maxValue = Number(maxValueDraft);
  const penaltyValue = Number(penaltyDraft);
  const selectedMetric = React.useMemo(
    () => metrics.find((metric) => metric.code === metricDraft) ?? metrics[0],
    [metricDraft]
  );
  const booleanEventTypes = React.useMemo(
    () => factTypes.filter((x) => x.valueType === 'boolean'),
    [factTypes]
  );
  const metricToFactTypeEventType = React.useMemo<Record<string, string>>(
    () => ({
      TEMPERATURE: 'TEMPERATURE_READING',
      MISSING_DOCS: 'DOCUMENT_MISSING',
      DOCUMENT_MISSING: 'DOCUMENT_MISSING'
    }),
    []
  );
  const selectedMetricDefinition = React.useMemo(() => {
    const mappedEventType = metricToFactTypeEventType[metricDraft] ?? metricDraft;
    return factTypes.find((item) => item.eventType === mappedEventType);
  }, [factTypes, metricDraft, metricToFactTypeEventType]);
  const isThresholdCondition = conditionTypeDraft === 'threshold';
  const isRangeCondition = conditionTypeDraft === 'range';
  const isBooleanCondition = conditionTypeDraft === 'boolean';
  const canCreateRule =
    Number.isFinite(penaltyValue) &&
    penaltyValue > 0 &&
    (isBooleanCondition
      ? eventTypeDraft.trim().length > 0
      : (isRangeCondition
        ? (Number.isFinite(minValue) && Number.isFinite(maxValue) && minValue <= maxValue)
        : (Number.isFinite(thresholdValue) && thresholdValue > 0)));
  const ruleThresholdFieldError =
    isThresholdCondition && errorRules && errorRules.includes(t.rules.threshold) ? errorRules : undefined;
  const ruleMinFieldError = isRangeCondition && errorRules && errorRules.includes(t.rules.minValue) ? errorRules : undefined;
  const ruleMaxFieldError = isRangeCondition && errorRules && errorRules.includes(t.rules.maxValue) ? errorRules : undefined;
  const ruleEventTypeFieldError = isBooleanCondition && errorRules && errorRules.includes(t.rules.eventType) ? errorRules : undefined;
  const rulePenaltyFieldError = errorRules && errorRules.includes(t.rules.penalty) ? errorRules : undefined;
  const getMetricLabel = React.useCallback((metricCode: string) => {
    const mappedEventType = metricToFactTypeEventType[metricCode] ?? metricCode;
    const fromRegistry = factTypes.find((item) => item.eventType === mappedEventType)?.displayName;
    if (fromRegistry) return fromRegistry;
    if (metricCode === 'DELIVERY_DELAY') return t.rules.metrics.deliveryDelay;
    if (metricCode === 'TEMPERATURE') return t.rules.metrics.temperature;
    if (metricCode === 'MISSING_DOCS') return t.rules.metrics.missingDocs;
    return metricCode;
  }, [factTypes, metricToFactTypeEventType]);

  const readCargoTypeScope = React.useCallback((scope: unknown): string | undefined => {
    if (!scope || typeof scope !== 'object' || Array.isArray(scope)) {
      return undefined;
    }
    const value = (scope as Record<string, unknown>).cargoType;
    return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
  }, []);

  const buildRuleDescription = React.useCallback(
    (input: {
      metric: string;
      conditionType: 'threshold' | 'range' | 'boolean';
      operator?: string;
      threshold?: string | number;
      minValue?: string | number | null;
      maxValue?: string | number | null;
      eventType?: string | null;
      cargoType?: string;
      penaltyAmount: string | number;
    }) => {
      let text = '';
      if (input.conditionType === 'threshold') {
        text = `${t.rules.if} ${getMetricLabel(input.metric)} ${input.operator ?? '>'} ${input.threshold ?? 0}`;
      } else if (input.conditionType === 'range') {
        text = `${t.rules.if} ${getMetricLabel(input.metric)} NOT IN [${input.minValue ?? 0} ... ${input.maxValue ?? 0}]`;
      } else {
        text = `${t.rules.if} ${input.eventType ?? 'DOCUMENT_MISSING'} occurred`;
      }

      if (input.cargoType && input.cargoType.trim().length > 0) {
        text += ` ${t.rules.and} cargoType = ${input.cargoType.trim()}`;
      }

      text += ` -> ${t.rules.penalty} ${input.penaltyAmount}`;
      return text;
    },
    [getMetricLabel]
  );

  const getTemplateDescription = React.useCallback((templateKey: string) => {
    if (templateKey === 'delivery_delay') return t.rules.templateDescriptionLateDelivery;
    if (templateKey === 'temperature') return t.rules.templateDescriptionTemperature;
    if (templateKey === 'missing_docs') return t.rules.templateDescriptionMissingDocs;
    return '';
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
      setCounterpartyDraft(result.contract.counterpartyId ?? '');
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

  const loadFactTypes = React.useCallback(async () => {
    try {
      const data = await getFactTypes();
      setFactTypes(data);
    } catch {
      setFactTypes([]);
    }
  }, []);

  React.useEffect(() => {
    void loadFactTypes();
  }, [loadFactTypes]);

  const loadCounterparties = React.useCallback(async () => {
    setLoadingCounterparties(true);
    try {
      const items = await getCounterparties();
      setCounterparties(items);
    } catch {
      setCounterparties([]);
    } finally {
      setLoadingCounterparties(false);
    }
  }, []);

  React.useEffect(() => {
    void loadCounterparties();
  }, [loadCounterparties]);

  const onStartEdit = React.useCallback(() => {
    setIsEditing(true);
    setSaveError(null);
    setNameDraft(data?.name ?? '');
    setCounterpartyDraft(data?.counterpartyId ?? '');
  }, [data]);

  const onCancelEdit = React.useCallback(() => {
    setIsEditing(false);
    setSaveError(null);
    setNameDraft(data?.name ?? '');
    setCounterpartyDraft(data?.counterpartyId ?? '');
  }, [data]);

  const onSave = React.useCallback(async () => {
    if (!id || !etag) {
      setSaveError(t.common.requestFailed);
      return;
    }
    if (!counterpartyDraft.trim()) {
      setSaveError(t.common.validationError);
      return;
    }

    setSaveLoading(true);
    setSaveError(null);
    try {
      await updateContract(id, { name: nameDraft.trim(), counterpartyId: counterpartyDraft }, etag);
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
  }, [id, etag, nameDraft, counterpartyDraft, loadContract]);

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

  React.useEffect(() => {
    if (templateDraft) return;
    const defaultConditionType = selectedMetricDefinition?.defaultConditionType;
    if (defaultConditionType === 'range' || defaultConditionType === 'boolean' || defaultConditionType === 'threshold') {
      setConditionTypeDraft(defaultConditionType);
    } else {
      setConditionTypeDraft('threshold');
    }
    if (defaultConditionType === 'boolean') {
      setEventTypeDraft(booleanEventTypes[0]?.eventType ?? 'DOCUMENT_MISSING');
    }
  }, [metricDraft, selectedMetricDefinition, templateDraft, booleanEventTypes]);

  React.useEffect(() => {
    if (!isBooleanCondition || booleanEventTypes.length === 0) {
      return;
    }
    const exists = booleanEventTypes.some((x) => x.eventType === eventTypeDraft);
    if (!exists) {
      setEventTypeDraft(booleanEventTypes[0].eventType);
    }
  }, [booleanEventTypes, eventTypeDraft, isBooleanCondition]);

  const onTemplateSelect = React.useCallback((templateKey: string) => {
    setTemplateDraft(templateKey);
    const template = ruleTemplates.find((item) => item.key === templateKey);
    if (!template) {
      return;
    }

    setMetricDraft(template.metric);
    setConditionTypeDraft(template.conditionType);
    setOperatorDraft(template.operator ?? '>');
    setThresholdDraft(template.threshold !== undefined ? String(template.threshold) : '');
    setMinValueDraft(template.minValue !== undefined ? String(template.minValue) : '');
    setMaxValueDraft(template.maxValue !== undefined ? String(template.maxValue) : '');
    setEventTypeDraft(template.eventType ?? 'DOCUMENT_MISSING');
  }, []);

  const onCreateRule = React.useCallback(async () => {
    if (!id || !activatedVersion) return;
    if (!canCreateRule) {
      setErrorRules(
        isRangeCondition
          ? `${t.common.validationError}: ${t.rules.minValue} <= ${t.rules.maxValue}, ${t.rules.penalty} > 0`
          : isBooleanCondition
          ? `${t.common.validationError}: ${t.rules.eventType}, ${t.rules.penalty} > 0`
          : `${t.common.validationError}: ${t.rules.threshold} > 0, ${t.rules.penalty} > 0`
      );
      return;
    }

    setRuleActionLoading(true);
    setErrorRules(null);
    try {
      await createSlaRule(id, activatedVersion.id, {
        metric: selectedMetric.code,
        conditionType: conditionTypeDraft,
        operator: isThresholdCondition ? operatorDraft : undefined,
        threshold: isThresholdCondition ? thresholdValue : undefined,
        minValue: isRangeCondition ? minValue : undefined,
        maxValue: isRangeCondition ? maxValue : undefined,
        eventType: isBooleanCondition ? eventTypeDraft.trim() : undefined,
        scope: cargoTypeScopeDraft.trim().length > 0 ? { cargoType: cargoTypeScopeDraft.trim() } : undefined,
        penaltyAmount: penaltyValue,
      });
      await loadRules();
      setMetricDraft('DELIVERY_DELAY');
      setConditionTypeDraft('threshold');
      setOperatorDraft('>');
      setThresholdDraft('');
      setMinValueDraft('');
      setMaxValueDraft('');
      setEventTypeDraft(booleanEventTypes[0]?.eventType ?? 'DOCUMENT_MISSING');
      setCargoTypeScopeDraft('');
      setPenaltyDraft('');
    } catch {
      setErrorRules(t.common.requestFailed);
    } finally {
      setRuleActionLoading(false);
    }
  }, [id, activatedVersion, canCreateRule, isRangeCondition, isBooleanCondition, conditionTypeDraft, isThresholdCondition, selectedMetric, operatorDraft, thresholdValue, minValue, maxValue, eventTypeDraft, cargoTypeScopeDraft, penaltyValue, loadRules, booleanEventTypes]);

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
              <div>
                {t.contracts.counterparty}:{' '}
                {data.counterpartyName ?? data.counterpartyId ?? t.common.noData}
              </div>
              <Button type="button" variant="secondary" onClick={onStartEdit}>
                {t.contracts.edit}
              </Button>
            </div>
          ) : (
            <div className="space-y-4">
              <FormField label={t.contracts.name}>
                <Input
                  value={nameDraft}
                  onChange={(ev) => setNameDraft(ev.target.value)}
                  disabled={saveLoading}
                  error={Boolean(saveError)}
                />
              </FormField>
              <FormField label={t.contracts.counterparty}>
                <select
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm"
                  value={counterpartyDraft}
                  onChange={(ev) => setCounterpartyDraft(ev.target.value)}
                  disabled={saveLoading || loadingCounterparties}
                >
                  <option value="">{t.contracts.selectCounterpartyPlaceholder}</option>
                  {counterparties.map((cp) => (
                    <option key={cp.id} value={cp.id}>
                      {cp.name}
                    </option>
                  ))}
                </select>
              </FormField>
              {loadingCounterparties ? <div className="text-sm text-gray-600">{t.common.loading}</div> : null}
              {saveError ? <div className="text-sm text-red-600">{saveError}</div> : null}
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="primary"
                  onClick={() => void onSave()}
                  disabled={saveLoading || !nameDraft.trim() || !counterpartyDraft.trim()}
                >
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
              <div className="space-y-2">
                <div className="text-sm font-medium text-gray-700">{t.rules.useTemplate}</div>
                <div className="flex gap-3 flex-wrap">
                  {ruleTemplates.map((template) => (
                    <button
                      key={template.key}
                      type="button"
                      onClick={() => onTemplateSelect(template.key)}
                      disabled={ruleActionLoading}
                      className={[
                        'border border-gray-200 rounded-lg p-3 cursor-pointer hover:border-blue-400 hover:bg-blue-50 transition text-left',
                        templateDraft === template.key ? 'border-blue-500 bg-blue-50' : ''
                      ].join(' ')}
                    >
                      <div className="font-medium">{template.name}</div>
                      <div className="text-sm text-gray-600">{getTemplateDescription(template.key)}</div>
                    </button>
                  ))}
                </div>
              </div>
              <FormField label={t.rules.metric}>
                <select
                  value={metricDraft}
                  onChange={(ev) => {
                    setTemplateDraft('');
                    setMetricDraft(ev.target.value);
                  }}
                  disabled={ruleActionLoading}
                >
                  {metrics.map((metric) => (
                    <option key={metric.code} value={metric.code}>
                      {getMetricLabel(metric.code)}
                    </option>
                  ))}
                </select>
              </FormField>
              <FormField label={t.rules.conditionType}>
                <select
                  value={conditionTypeDraft}
                  onChange={(ev) => setConditionTypeDraft(ev.target.value as 'threshold' | 'range' | 'boolean')}
                  disabled={ruleActionLoading}
                >
                  <option value="threshold">{t.rules.threshold}</option>
                  <option value="range">{t.rules.range}</option>
                  <option value="boolean">{t.rules.boolean}</option>
                </select>
              </FormField>
              {isThresholdCondition ? (
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
                      <span>{selectedMetricDefinition?.unit ?? selectedMetric.unit}</span>
                    </div>
                  </FormField>
                </>
              ) : isRangeCondition ? (
                <>
                  <FormField label={t.rules.minValue} error={ruleMinFieldError}>
                    <div className="flex items-center gap-2">
                      <Input
                        type="number"
                        value={minValueDraft}
                        onChange={(ev) => setMinValueDraft(ev.target.value)}
                        disabled={ruleActionLoading}
                        className="w-24"
                        error={Boolean(ruleMinFieldError)}
                      />
                      <span>{selectedMetricDefinition?.unit ?? selectedMetric.unit}</span>
                    </div>
                  </FormField>
                  <FormField label={t.rules.maxValue} error={ruleMaxFieldError}>
                    <div className="flex items-center gap-2">
                      <Input
                        type="number"
                        value={maxValueDraft}
                        onChange={(ev) => setMaxValueDraft(ev.target.value)}
                        disabled={ruleActionLoading}
                        className="w-24"
                        error={Boolean(ruleMaxFieldError)}
                      />
                      <span>{selectedMetricDefinition?.unit ?? selectedMetric.unit}</span>
                    </div>
                  </FormField>
                </>
              ) : (
                <FormField label={t.rules.eventType} error={ruleEventTypeFieldError}>
                  <select
                    value={eventTypeDraft}
                    onChange={(ev) => setEventTypeDraft(ev.target.value)}
                    disabled={ruleActionLoading}
                  >
                    {booleanEventTypes.map((item) => (
                      <option key={item.eventType} value={item.eventType}>
                        {item.eventType}
                      </option>
                    ))}
                    {booleanEventTypes.length === 0 ? <option value="DOCUMENT_MISSING">DOCUMENT_MISSING</option> : null}
                  </select>
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
              <FormField label="cargoType (scope)">
                <Input
                  value={cargoTypeScopeDraft}
                  onChange={(ev) => setCargoTypeScopeDraft(ev.target.value)}
                  disabled={ruleActionLoading}
                  placeholder="ICE_CREAM"
                />
              </FormField>
              <div className="bg-gray-50 border rounded p-3 text-sm text-gray-700">
                {t.rules.preview}:{' '}
                {buildRuleDescription({
                  metric: selectedMetric.code,
                  conditionType: conditionTypeDraft,
                  operator: operatorDraft,
                  threshold: thresholdDraft || '0',
                  minValue: minValueDraft || '0',
                  maxValue: maxValueDraft || '0',
                  eventType: eventTypeDraft || 'DOCUMENT_MISSING',
                  cargoType: cargoTypeScopeDraft || undefined,
                  penaltyAmount: penaltyDraft || '0'
                })}
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
            {errorRules && !rulePenaltyFieldError && !ruleThresholdFieldError && !ruleMinFieldError && !ruleMaxFieldError && !ruleEventTypeFieldError ? (
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
                    <th>Description</th>
                    <th>{t.rules.penalty}</th>
                    <th>{t.contracts.actions}</th>
                  </tr>
                </thead>
                <tbody>
                  {rules.map((rule) => (
                    <tr key={rule.id}>
                      <td>{rule.metric}</td>
                      <td>{rule.conditionType === 'range' ? t.rules.range : (rule.conditionType === 'boolean' ? t.rules.boolean : rule.operator)}</td>
                      <td>
                        {rule.conditionType === 'range'
                          ? `[${rule.minValue ?? ''} ... ${rule.maxValue ?? ''}]`
                          : (rule.conditionType === 'boolean' ? `${t.rules.eventType}: ${rule.eventType ?? 'DOCUMENT_MISSING'}` : rule.threshold)}
                      </td>
                      <td>
                        {buildRuleDescription({
                          metric: rule.metric,
                          conditionType: rule.conditionType ?? 'threshold',
                          operator: rule.operator,
                          threshold: rule.threshold,
                          minValue: rule.minValue,
                          maxValue: rule.maxValue,
                          eventType: rule.eventType,
                          cargoType: readCargoTypeScope(rule.scope),
                          penaltyAmount: rule.penaltyAmount
                        })}
                      </td>
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

