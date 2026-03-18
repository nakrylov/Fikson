import React, { useCallback, useEffect, useState } from 'react';
import { ApiError, FactImport, getFactImports, uploadFacts } from '../api/api';
import { t } from '../i18n';

export function FactImportPage() {
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploadResult, setUploadResult] = useState<{ imported: number; claimsGenerated: number } | null>(null);

  const [items, setItems] = useState<FactImport[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);

  const loadHistory = useCallback(async () => {
    setLoadingHistory(true);
    setHistoryError(null);
    try {
      const data = await getFactImports();
      setItems(data);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        if (err.status === 403) setHistoryError(t.common.forbidden);
        else if (err.status >= 500) setHistoryError(t.common.serverError);
        else setHistoryError(t.common.requestFailed);
      } else {
        setHistoryError(t.common.requestFailed);
      }
    } finally {
      setLoadingHistory(false);
    }
  }, []);

  useEffect(() => {
    void loadHistory();
  }, [loadHistory]);

  const onUpload = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      if (!file) {
        setUploadError(t.common.validationError);
        return;
      }

      setUploading(true);
      setUploadError(null);
      setUploadResult(null);
      try {
        const result = await uploadFacts(file);
        setUploadResult(result);
        setFile(null);
        await loadHistory();
      } catch (err: unknown) {
        if (err instanceof ApiError) {
          if (err.status === 400) setUploadError(t.common.validationError);
          else if (err.status === 403) setUploadError(t.common.forbidden);
          else if (err.status >= 500) setUploadError(t.common.serverError);
          else setUploadError(t.common.requestFailed);
        } else {
          setUploadError(t.common.requestFailed);
        }
      } finally {
        setUploading(false);
      }
    },
    [file, loadHistory]
  );

  return (
    <div>
      <h2>{t.imports.title}</h2>

      <form onSubmit={onUpload}>
        <label>
          <div>{t.imports.selectFile}</div>
          <input
            type="file"
            accept=".csv,text/csv"
            disabled={uploading}
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
        </label>
        <div style={{ marginTop: 8 }}>
          <button type="submit" disabled={uploading || !file}>
            {uploading ? t.imports.uploading : t.imports.upload}
          </button>
        </div>
        {uploadError ? <div style={{ color: 'red' }}>{uploadError}</div> : null}
        {uploadResult ? (
          <div>
            {t.imports.rows}: {uploadResult.imported}; {t.imports.claims}: {uploadResult.claimsGenerated}
          </div>
        ) : null}
      </form>

      <h3 style={{ marginTop: 16 }}>{t.imports.historyTitle}</h3>
      {loadingHistory ? <div>{t.common.loading}</div> : null}
      {historyError ? <div style={{ color: 'red' }}>{historyError}</div> : null}
      {!loadingHistory && !historyError && items.length === 0 ? <div>{t.imports.empty}</div> : null}

      {!loadingHistory && !historyError && items.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>{t.imports.file}</th>
              <th>{t.imports.rows}</th>
              <th>{t.imports.claims}</th>
              <th>{t.imports.date}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>{item.fileName}</td>
                <td>{item.rowsImported}</td>
                <td>{item.claimsGenerated}</td>
                <td>{new Date(item.createdAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
