import { useEffect, useState, type FormEvent } from 'react';
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
import {
  MAX_SIGNED_ARTIFACT_BYTES,
  UPLOADABLE_SIGNATURE_STATUSES,
  type SignedArtifactDto,
  type UploadableSignatureStatus,
} from '../types';

const STATUS_LABELS: Record<UploadableSignatureStatus, string> = {
  PartiallySigned: 'Partially signed',
  Signed: 'Signed',
};

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
  const [artifacts, setArtifacts] = useState<SignedArtifactDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [file, setFile] = useState<File | null>(null);
  const [signerSummary, setSignerSummary] = useState('');
  const [status, setStatus] = useState<UploadableSignatureStatus>('Signed');
  const [fileError, setFileError] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [downloadingId, setDownloadingId] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const res = await listSignedArtifacts(versionId);
        if (!active) return;
        if (res.success && res.data) setArtifacts(res.data);
        else setLoadError(res.message ?? 'Could not load signed artifacts.');
      } catch (err) {
        if (active) setLoadError(apiErrorMessage(err, 'Could not load signed artifacts.'));
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
      setFileError('Only a PDF file can be attached.');
      return;
    }
    if (picked && picked.size > MAX_SIGNED_ARTIFACT_BYTES) {
      setFile(null);
      setFileError('The file must be 20 MB or smaller.');
      return;
    }
    setFile(picked);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) {
      setFileError('Choose a PDF file to attach.');
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
        const input = document.getElementById('signed-artifact-file') as HTMLInputElement | null;
        if (input) input.value = '';
      } else {
        setUploadError(res.message ?? 'Could not attach the signed PDF.');
      }
    } catch (err) {
      setUploadError(apiErrorMessage(err, 'Could not attach the signed PDF.'));
    } finally {
      setIsUploading(false);
    }
  };

  const handleDownload = async (artifactId: number) => {
    setDownloadingId(artifactId);
    try {
      const res = await getSignedArtifactDownloadUrl(artifactId);
      if (res.success && res.data?.url) {
        window.open(res.data.url, '_blank', 'noopener,noreferrer');
      }
    } finally {
      setDownloadingId(null);
    }
  };

  return (
    <Card data-testid="signed-artifacts-panel">
      <h2 className="mb-3 font-serif text-lg text-brand-slate-800">Signed artifacts</h2>

      <form onSubmit={handleSubmit} className="mb-5 space-y-3" data-testid="signed-artifact-upload-form">
        {(fileError || uploadError) && (
          <div role="alert">
            <Notice variant="error" title={fileError ?? uploadError ?? ''} />
          </div>
        )}
        <div>
          <label htmlFor="signed-artifact-file" className="mb-1 block text-[13px] font-medium text-brand-slate-600">
            Signed PDF (up to 20 MB)
          </label>
          <input
            id="signed-artifact-file"
            type="file"
            accept="application/pdf"
            onChange={handleFileChange}
            data-testid="signed-artifact-file"
            className="block w-full text-sm text-brand-slate-600"
          />
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <Input
            label="Signer summary (optional)"
            value={signerSummary}
            onChange={(e) => setSignerSummary(e.target.value)}
            maxLength={500}
            placeholder="e.g. Parent and LEA rep signed"
            data-testid="signed-artifact-signer-summary"
          />
          <Select
            label="Status *"
            value={status}
            onChange={(e) => setStatus(e.target.value as UploadableSignatureStatus)}
            data-testid="signed-artifact-status"
          >
            {UPLOADABLE_SIGNATURE_STATUSES.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABELS[s]}
              </option>
            ))}
          </Select>
        </div>
        <Button type="submit" size="sm" loading={isUploading} data-testid="signed-artifact-submit">
          Attach signed PDF
        </Button>
      </form>

      {loadError && (
        <div role="alert">
          <Notice variant="error" title={loadError} />
        </div>
      )}

      {!loadError && isLoading && (
        <div className="space-y-2">
          <Skeleton className="h-8 w-full" />
        </div>
      )}

      {!loadError && !isLoading && artifacts.length === 0 && (
        <p className="text-sm text-brand-slate-400" data-testid="signed-artifacts-empty">
          No signed copies attached yet.
        </p>
      )}

      {!loadError && !isLoading && artifacts.length > 0 && (
        <ul className="divide-y divide-brand-slate-100" data-testid="signed-artifacts-list">
          {artifacts.map((a) => (
            <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <div>
                <p className="text-sm font-medium text-brand-slate-800">{a.fileName}</p>
                <p className="text-xs text-brand-slate-500">
                  {formatFileSize(a.sizeBytes)} · Uploaded {formatDate(a.uploadedAt)}
                  {a.uploadedByName ? ` by ${a.uploadedByName}` : ''}
                  {a.signerSummary ? ` · ${a.signerSummary}` : ''}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant="neutral">{a.contentType === 'application/pdf' ? 'PDF' : a.contentType}</Badge>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => handleDownload(a.id)}
                  loading={downloadingId === a.id}
                  data-testid={`signed-artifact-download-${a.id}`}
                >
                  Download
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
