import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { getConsentDownloadUrl, receiveConsent, requestConsent } from '../api/evaluation-api';
import type { EvaluationCaseDto } from '../types';

interface ConsentSectionProps {
  studentId: number;
  evaluation: EvaluationCaseDto;
  onChanged: (updated: EvaluationCaseDto) => void;
}

/** Consent request (date-stamp only) → receive (date + optional PDF) →
 *  download link, once uploaded. */
export function ConsentSection({ studentId, evaluation, onChanged }: ConsentSectionProps) {
  const { t } = useTranslation('evaluation');
  const [isRequesting, setIsRequesting] = useState(false);
  const [receivedAt, setReceivedAt] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [isReceiving, setIsReceiving] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleRequest = async () => {
    setIsRequesting(true);
    setError(null);
    try {
      const res = await requestConsent(studentId);
      if (res.success && res.data) onChanged(res.data);
      else setError(res.message ?? t('consentSection.requestFailed'));
    } catch (err) {
      setError(apiErrorMessage(err, t('consentSection.requestFailed')));
    } finally {
      setIsRequesting(false);
    }
  };

  const handleReceive = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!receivedAt) {
      setError(t('consentSection.receivedDateRequired'));
      return;
    }
    setIsReceiving(true);
    setError(null);
    try {
      const res = await receiveConsent(studentId, receivedAt, file ?? undefined);
      if (res.success && res.data) {
        onChanged(res.data);
        setReceivedAt('');
        setFile(null);
        if (fileInputRef.current) fileInputRef.current.value = '';
      } else {
        setError(res.message ?? t('consentSection.recordFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('consentSection.recordFailed')));
    } finally {
      setIsReceiving(false);
    }
  };

  const handleDownload = async () => {
    setIsDownloading(true);
    setError(null);
    try {
      const res = await getConsentDownloadUrl(studentId);
      if (res.success && res.data) {
        window.open(res.data, '_blank', 'noopener,noreferrer');
      } else {
        setError(res.message ?? t('consentSection.downloadFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('consentSection.downloadFailed')));
    } finally {
      setIsDownloading(false);
    }
  };

  return (
    <div className="space-y-3" data-testid="evaluation-consent-section">
      <h3 className="text-sm font-medium text-brand-slate-600">{t('consentSection.heading')}</h3>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      {!evaluation.consentRequestedAt && (
        <Button
          variant="secondary"
          size="sm"
          onClick={handleRequest}
          loading={isRequesting}
          data-testid="evaluation-consent-request"
        >
          {t('consentSection.requestButton')}
        </Button>
      )}

      {evaluation.consentRequestedAt && !evaluation.consentReceivedAt && (
        <form onSubmit={handleReceive} className="space-y-3">
          <p className="text-sm text-brand-slate-500">
            {t('consentSection.consentRequestedOn', { date: formatDate(evaluation.consentRequestedAt) })}
          </p>
          <div className="grid gap-3 sm:grid-cols-2">
            <Input
              label={t('consentSection.receivedDateLabel')}
              type="date"
              required
              value={receivedAt}
              onChange={(e) => setReceivedAt(e.target.value)}
              data-testid="evaluation-consent-received-date"
            />
            <div>
              <label className="mb-1 block text-[13px] font-medium text-brand-slate-600" htmlFor="evaluation-consent-file">
                {t('consentSection.fileLabel')}
              </label>
              <input
                id="evaluation-consent-file"
                ref={fileInputRef}
                type="file"
                accept="application/pdf"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                className="block w-full text-sm text-brand-slate-600"
                data-testid="evaluation-consent-file"
              />
            </div>
          </div>
          <Button type="submit" size="sm" loading={isReceiving} data-testid="evaluation-consent-receive-submit">
            {t('consentSection.recordReceivedButton')}
          </Button>
        </form>
      )}

      {evaluation.consentReceivedAt && (
        <div className="flex flex-wrap items-center gap-3 text-sm text-brand-slate-600">
          <span>{t('consentSection.consentReceivedOn', { date: formatDate(evaluation.consentReceivedAt) })}</span>
          {evaluation.hasConsentDocument && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleDownload}
              loading={isDownloading}
              data-testid="evaluation-consent-download"
            >
              {t('consentSection.downloadButton', {
                name: evaluation.consentFileName ?? t('consentSection.defaultFileName'),
              })}
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
