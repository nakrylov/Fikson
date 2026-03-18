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
  const [metricDraft, setMetricDraft] = React.useState<string>('');
  const [operatorDraft, setOperatorDraft] = React.useState<string>('>');
  const [thresholdDraft, setThresholdDraft] = React.useState<string>('0');
  const [penaltyDraft, setPenaltyDraft] = React.useState<string>('0');

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
    setRuleActionLoading(true);
    setErrorRules(null);
    try {
      await createSlaRule(id, activatedVersion.id, {
        metric: metricDraft,
        operator: operatorDraft,
        threshold: Number(thresholdDraft),
        penaltyAmount: Number(penaltyDraft)
      });
      await loadRules();
    } catch {
      setErrorRules(t.common.requestFailed);
    } finally {
      setRuleActionLoading(false);
    }
  }, [id, activatedVersion, metricDraft, operatorDraft, thresholdDraft, penaltyDraft, loadRules]);

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
    <div>
      <h2>{t.contracts.detailsTitle}</h2>
      <div>
        <Link to="/contracts">{t.common.backToList}</Link>
        <button type="button" onClick={() => void loadContract()}>
          {t.common.refresh}
        </button>
      </div>

      {loading ? <div>{t.common.loading}</div> : null}
      {error ? <div style={{ color: 'red' }}>{error}</div> : null}

      {data ? (
        <div style={{ marginTop: 12 }}>
          {!isEditing ? (
            <div>
              <div>
                {t.contracts.name}: {data.name}
              </div>
              <button type="button" onClick={onStartEdit}>
                {t.contracts.edit}
              </button>
            </div>
          ) : (
            <div>
              <label style={{ display: 'block' }}>
                <div>{t.contracts.name}</div>
                <input
                  value={nameDraft}
                  onChange={(ev) => setNameDraft(ev.target.value)}
                  disabled={saveLoading}
                />
              </label>
              <button type="button" onClick={() => void onSave()} disabled={saveLoading}>
                {t.contracts.save}
              </button>
              <button type="button" onClick={onCancelEdit} disabled={saveLoading}>
                {t.contracts.cancel}
              </button>
              {saveError ? <div style={{ color: 'red' }}>{saveError}</div> : null}
            </div>
          )}
        </div>
      ) : null}

      <div style={{ marginTop: 16 }}>
        <h3>{t.contracts.versions}</h3>
        <button type="button" onClick={() => void onCreateVersion()} disabled={versionActionLoading}>
          {t.contracts.createVersion}
        </button>

        {loadingVersions ? <div>{t.common.loading}</div> : null}
        {errorVersions ? <div style={{ color: 'red' }}>{errorVersions}</div> : null}

        {versions.length > 0 ? (
          <table>
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
                  <td>
                    {v.status === 'Draft' ? (
                      <button
                        type="button"
                        onClick={() => void onSignVersion(v.id)}
                        disabled={versionActionLoading}
                      >
                        {t.contracts.signVersion}
                      </button>
                    ) : null}
                    {v.status === 'Signed' ? (
                      <button
                        type="button"
                        onClick={() => void onActivateVersion(v.id)}
                        disabled={versionActionLoading}
                      >
                        {t.contracts.activateVersion}
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </div>

      <div style={{ marginTop: 16 }}>
        <h3>{t.dashboard.title}</h3>
        {loadingDashboard ? <div>{t.common.loading}</div> : null}
        {errorDashboard ? <div style={{ color: 'red' }}>{errorDashboard}</div> : null}
        {dashboard ? (
          <div>
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
      </div>

      {activatedVersion ? (
        <div style={{ marginTop: 16 }}>
          <h3>{t.rules.title}</h3>

          <div>
            <label>
              <div>{t.rules.metric}</div>
              <input
                value={metricDraft}
                onChange={(ev) => setMetricDraft(ev.target.value)}
                disabled={ruleActionLoading}
              />
            </label>
            <label>
              <div>{t.rules.operator}</div>
              <select
                value={operatorDraft}
                onChange={(ev) => setOperatorDraft(ev.target.value)}
                disabled={ruleActionLoading}
              >
                <option value=">">{'>'}</option>
                <option value=">=">{'>='}</option>
                <option value="<">{'<'}</option>
                <option value="<=">{'<='}</option>
              </select>
            </label>
            <label>
              <div>{t.rules.threshold}</div>
              <input
                type="number"
                value={thresholdDraft}
                onChange={(ev) => setThresholdDraft(ev.target.value)}
                disabled={ruleActionLoading}
              />
            </label>
            <label>
              <div>{t.rules.penalty}</div>
              <input
                type="number"
                value={penaltyDraft}
                onChange={(ev) => setPenaltyDraft(ev.target.value)}
                disabled={ruleActionLoading}
              />
            </label>
            <button type="button" onClick={() => void onCreateRule()} disabled={ruleActionLoading}>
              {t.rules.create}
            </button>
          </div>

          {loadingRules ? <div>{t.common.loading}</div> : null}
          {errorRules ? <div style={{ color: 'red' }}>{errorRules}</div> : null}

          {!loadingRules && !errorRules && rules.length === 0 ? <div>{t.rules.empty}</div> : null}

          {rules.length > 0 ? (
            <table>
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
                      <button
                        type="button"
                        onClick={() => void onDeleteRule(rule.id)}
                        disabled={ruleActionLoading}
                      >
                        {t.rules.delete}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>
      ) : null}

      <pre style={{ whiteSpace: 'pre-wrap' }}>{data ? JSON.stringify(data, null, 2) : t.common.noData}</pre>
    </div>
  );
}

