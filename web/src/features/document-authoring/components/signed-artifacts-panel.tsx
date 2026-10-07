import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import {
  getSignedArtifactDownloadUrl,
  listSignedArtifacts,
  uploadSignedArtifact,
} from '../api/documents-api';
import { signatureStatusLabel } from '../lib/signature-status-label';
import {
  MAX_SIGNED_ARTIFACT_BYTES,
  UPLOADABLE_SIGNATURE_STATUSES,
  type SignedArtifactDto,
  type UploadableSignatureStatus,
} from '../types';

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

interface SignedArtifactsPanelProps {
  versionId: number;
  /** Called after a successful upload so the parent can refresh the version's
   *  signatureStatus/signedArtifactCount (not part of the artifact list itself).
   *  `status` is exactly what was submitted — the server sets the version's
   *  SignatureStatus to it verbatim, rather than computing an aggregate. */
  onUploaded: (artifact: SignedArtifactDto, status: UploadableSignatureStatus) => void;
}

/**
 * Attach a scanned/uploaded signed PDF to a finalized version, and list what's
 * already attached with a short-lived download link for each (plan 7, decision 4).
 * Typed-name e-sign is explicitly not offered here — only an upload of an
 * externally-signed copy.
 */
export function SignedArtifactsPanel({ versionId, onUploaded }: SignedArtifactsPanelProps) {
  const { t } = useTranslation('document-authoring');
  const [artifacts, setArtifacts] = useState<SignedArtifactDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // A server message is already resolved text; the generic case is
  // translated at render time below — so the mount-fetch effect never needs
  // `t` in its dependency array (a language switch must not re-trigger it).
  const [loadError, setLoadError] = useState<{ kind: 'server'; message: string } | { kind: 'generic' } | null>(null);

  const [file, setFile] = useState<File | null>(null);
  const [signerSummary, setSignerSummary] = useState('');
  const [status, setStatus] = useState<UploadableSignatureStatus>('Signed');
  const [fileError, setFileError] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [downloadingId, setDownloadingId] = useState<number | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const res = await listSignedArtifacts(versionId);
        if (!active) return;
        if (res.success && res.data) setArtifacts(res.data);
        else setLoadError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      } catch (err) {
        if (active) {
          const message = apiErrorMessage(err, '');
          setLoadError(message ? { kind: 'server', message } : { kind: 'generic' });
        }
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [versionId]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const picked = e.target.files?.[0] ?? null;
    setFileError(null);
    if (picked && picked.type !== 'application/pdf') {
      setFile(null);
      setFileError(t('signedArtifactsPanel.pdfOnlyError'));
      return;
    }
    if (picked && picked.size > MAX_SIGNED_ARTIFACT_BYTES) {
      setFile(null);
      setFileError(t('signedArtifactsPanel.tooLargeError'));
      return;
    }
    setFile(picked);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) {
      setFileError(t('signedArtifactsPanel.chooseFileError'));
      return;
    }
    setIsUploading(true);
    setUploadError(null);
    try {
      const res = await uploadSignedArtifact(versionId, file, status, signerSummary.trim() || undefined);
      if (res.success && res.data) {
        setArtifacts((prev) => [res.data!, ...prev]);
        onUploaded(res.data, status);
        setFile(null);
        setSignerSummary('');
        if (fileInputRef.current) fileInputRef.current.value = '';
      } else {
        setUploadError(res.message ?? t('signedArtifactsPanel.uploadGenericError'));
      }
    } catch (err) {
      setUploadError(apiErrorMessage(err, t('signedArtifactsPanel.uploadGenericError')));
    } finally {
      setIsUploading(false);
    }
  };

  const handleDownload = async (artifactId: number) => {
    setDownloadingId(artifactId);
    setDownloadError(null);
    try {
      const res = await getSignedArtifactDownloadUrl(artifactId);
      if (res.success && res.data?.url) {
        window.open(res.data.url, '_blank', 'noopener,noreferrer');
      } else {
        setDownloadError(res.message ?? t('signedArtifactsPanel.downloadGenericError'));
      }
    } catch (err) {
      setDownloadError(apiErrorMessage(err, t('signedArtifactsPanel.downloadGenericError')));
    } finally {
      setDownloadingId(null);
    }
  };

  return (
    <Card data-testid="signed-artifacts-panel">
      <h2 className="mb-3 font-serif text-lg text-brand-slate-800">{t('signedArtifactsPanel.heading')}</h2>

      <form onSubmit={handleSubmit} className="mb-5 space-y-3" data-testid="signed-artifact-upload-form">
        {(fileError || uploadError) && (
          <div role="alert">
            <Notice variant="error" title={fileError ?? uploadError ?? ''} />
          </div>
        )}
        <div>
          <label htmlFor="signed-artifact-file" className="mb-1 block text-[13px] font-medium text-brand-slate-600">
            {t('signedArtifactsPanel.fileLabel')}
          </label>
          <input
            ref={fileInputRef}
            id="signed-artifact-file"
            type="file"
            accept="application/pdf"
            onChange={handleFileChange}
            disabled={isUploading}
            data-testid="signed-artifact-file"
            className="block w-full text-sm text-brand-slate-600"
          />
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <Input
            label={t('signedArtifactsPanel.signerSummaryLabel')}
            value={signerSummary}
            onChange={(e) => setSignerSummary(e.target.value)}
            maxLength={500}
            placeholder={t('signedArtifactsPanel.signerSummaryPlaceholder')}
            data-testid="signed-artifact-signer-summary"
          />
          <Select
            label={t('signedArtifactsPanel.statusLabel')}
            value={status}
            onChange={(e) => setStatus(e.target.value as UploadableSignatureStatus)}
            data-testid="signed-artifact-status"
          >
            {UPLOADABLE_SIGNATURE_STATUSES.map((s) => (
              <option key={s} value={s}>
                {signatureStatusLabel(s)}
              </option>
            ))}
          </Select>
        </div>
        <Button type="submit" size="sm" loading={isUploading} data-testid="signed-artifact-submit">
          {t('signedArtifactsPanel.attachButton')}
        </Button>
      </form>

      {loadError && (
        <div role="alert">
          <Notice
            variant="error"
            title={loadError.kind === 'server' ? loadError.message : t('signedArtifactsPanel.loadError')}
          />
        </div>
      )}

      {!loadError && isLoading && (
        <div className="space-y-2">
          <Skeleton className="h-8 w-full" />
        </div>
      )}

      {!loadError && !isLoading && artifacts.length === 0 && (
        <p className="text-sm text-brand-slate-500" data-testid="signed-artifacts-empty">
          {t('signedArtifactsPanel.emptyState')}
        </p>
      )}

      {!loadError && !isLoading && artifacts.length > 0 && (
        <>
        {downloadError && (
          <div role="alert" className="mb-2">
            <Notice variant="error" title={downloadError} />
          </div>
        )}
        <ul className="divide-y divide-brand-slate-100" data-testid="signed-artifacts-list">
          {artifacts.map((a) => (
            <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <div>
                <p className="text-sm font-medium text-brand-slate-800">{a.fileName}</p>
                <p className="text-xs text-brand-slate-500">
                  {formatFileSize(a.sizeBytes)} · {t('signedArtifactsPanel.uploaded', { date: formatDate(a.uploadedAt) })}
                  {a.uploadedByName ? t('signedArtifactsPanel.uploadedBy', { name: a.uploadedByName }) : ''}
                  {a.signerSummary ? ` · ${a.signerSummary}` : ''}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant="neutral">{a.contentType === 'application/pdf' ? t('signedArtifactsPanel.contentTypePdf') : a.contentType}</Badge>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => handleDownload(a.id)}
                  loading={downloadingId === a.id}
                  data-testid={`signed-artifact-download-${a.id}`}
                >
                  {t('signedArtifactsPanel.download')}
                </Button>
              </div>
            </li>
          ))}
        </ul>
        </>
      )}
    </Card>
  );
}
