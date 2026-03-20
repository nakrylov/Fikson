import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError, downloadFactImportTemplate, FactImport, getFactImports, uploadFacts } from '../api/api';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { FormField } from '../components/FormField';
import { Column, Table } from '../components/Table';
import { t } from '../i18n';

const REQUIRED_HEADERS = ['shipmentId', 'factType', 'eventTime', 'value'] as const;

export function FactImportPage() {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewRows, setPreviewRows] = useState<string[][]>([]);
  const [headers, setHeaders] = useState<string[]>([]);
  const [missingHeaders, setMissingHeaders] = useState<string[]>([]);
  const [parseError, setParseError] = useState<string | null>(null);
  const [isPreviewReady, setIsPreviewReady] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [downloadingTemplate, setDownloadingTemplate] = useState(false);
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

  const importColumns = useMemo<Column<FactImport>[]>(() => [
    { key: 'fileName', title: t.imports.file },
    { key: 'rowsImported', title: t.imports.rows },
    { key: 'claimsGenerated', title: t.imports.claims },
    {
      key: 'createdAt',
      title: t.imports.date,
      render: (item) => new Date(item.createdAt).toLocaleString()
    }
  ], []);

  const previewTableColumns = useMemo<Column<Record<string, string>>[]>(
    () => headers.map((header) => ({ key: header, title: header })),
    [headers]
  );

  const previewTableData = useMemo<Record<string, string>[]>(
    () =>
      previewRows.map((row) =>
        headers.reduce<Record<string, string>>((acc, header, index) => {
          acc[header] = row[index] ?? '';
          return acc;
        }, {})
      ),
    [headers, previewRows]
  );

  const parseCsvFile = useCallback((file: File) => {
    setParseError(null);
    setIsPreviewReady(false);
    setHeaders([]);
    setMissingHeaders([]);
    setPreviewRows([]);

    const reader = new FileReader();
    reader.onload = () => {
      try {
        const text = String(reader.result ?? '');
        const lines = text
          .split('\n')
          .map((line) => line.replace(/\r$/, '').trim())
          .filter((line) => line.length > 0);

        if (lines.length === 0) {
          setParseError(t.imports.invalidFormat);
          return;
        }

        const delimiter = lines[0].includes(';') ? ';' : ',';
        const parsedHeaders = lines[0].split(delimiter).map((cell) => cell.trim());
        const missing = REQUIRED_HEADERS.filter((required) => !parsedHeaders.includes(required));
        if (missing.length > 0) {
          setMissingHeaders([...missing]);
          setParseError(t.imports.invalidFormat);
          return;
        }

        const dataRows = lines
          .slice(1, 21)
          .map((line) => line.split(delimiter).map((cell) => cell.trim()));

        setHeaders(parsedHeaders);
        setPreviewRows(dataRows);
        setIsPreviewReady(true);
      } catch {
        setParseError(t.imports.invalidFormat);
      }
    };
    reader.onerror = () => setParseError(t.imports.invalidFormat);
    reader.readAsText(file);
  }, []);

  const onSelectFile = useCallback((nextFile: File | null) => {
    setSelectedFile(nextFile);
    setUploadResult(null);
    setUploadError(null);
    if (!nextFile) {
      setParseError(null);
      setIsPreviewReady(false);
      setHeaders([]);
      setMissingHeaders([]);
      setPreviewRows([]);
      return;
    }
    parseCsvFile(nextFile);
  }, [parseCsvFile]);

  const onUpload = useCallback(
    async () => {
      if (!selectedFile || parseError || !isPreviewReady) {
        setUploadError(t.common.validationError);
        return;
      }

      setUploading(true);
      setUploadError(null);
      setUploadResult(null);
      try {
        const result = await uploadFacts(selectedFile);
        setUploadResult(result);
        onSelectFile(null);
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
    [isPreviewReady, loadHistory, onSelectFile, parseError, selectedFile]
  );

  const onDownloadTemplate = useCallback(async () => {
    setUploadError(null);
    setDownloadingTemplate(true);
    try {
      await downloadFactImportTemplate();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        if (err.status === 403) setUploadError(t.common.forbidden);
        else if (err.status >= 500) setUploadError(t.common.serverError);
        else setUploadError(t.common.requestFailed);
      } else {
        setUploadError(t.common.requestFailed);
      }
    } finally {
      setDownloadingTemplate(false);
    }
  }, []);

  return (
    <div className="space-y-6">
      <Card
        title={t.imports.title}
        actions={(
          <Button type="button" variant="secondary" onClick={onDownloadTemplate} disabled={downloadingTemplate}>
            {t.imports.downloadTemplate}
          </Button>
        )}
      >
        <div className="space-y-4">
          <FormField label={t.imports.selectFile} error={uploadError ?? undefined}>
            <input
              type="file"
              accept=".csv,text/csv"
              disabled={uploading}
              onChange={(e) => onSelectFile(e.target.files?.[0] ?? null)}
            />
          </FormField>
          <div className="text-sm text-gray-600">{t.imports.previewHint}</div>
          {parseError ? (
            <div className="space-y-2 text-sm text-red-600">
              <div>{parseError}</div>
              {missingHeaders.length > 0 ? (
                <div className="space-y-1">
                  <div className="font-medium">{t.imports.missingColumnsLabel}</div>
                  <div className="flex flex-wrap gap-2">
                    {missingHeaders.map((header) => (
                      <span key={header} className="rounded bg-red-100 px-2 py-1 text-xs text-red-700">
                        {header}
                      </span>
                    ))}
                  </div>
                </div>
              ) : null}
            </div>
          ) : null}
          {isPreviewReady && !parseError && previewTableColumns.length > 0 ? (
            <div className="space-y-3">
              <div className="text-sm font-medium text-gray-700">{t.imports.previewTitle}</div>
              <Table columns={previewTableColumns} data={previewTableData} emptyText={t.common.noData} />
            </div>
          ) : null}
          {isPreviewReady && !parseError ? (
            <div className="flex gap-2">
              <Button type="button" variant="primary" className="w-full" disabled={uploading} onClick={onUpload}>
                {uploading ? t.imports.uploading : t.imports.confirmImport}
              </Button>
            </div>
          ) : null}
          {uploadResult ? (
            <div>
              {t.imports.rows}: {uploadResult.imported}; {t.imports.claims}: {uploadResult.claimsGenerated}
            </div>
          ) : null}
        </div>
      </Card>

      <Card title={t.imports.historyTitle}>
        {loadingHistory ? <div>{t.common.loading}</div> : null}
        {historyError ? <div style={{ color: 'red' }}>{historyError}</div> : null}
        {!loadingHistory && !historyError ? <Table columns={importColumns} data={items} emptyText={t.imports.empty} /> : null}
      </Card>
    </div>
  );
}
