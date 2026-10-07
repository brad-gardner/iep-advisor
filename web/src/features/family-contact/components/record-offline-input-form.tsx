import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { useDocumentList } from '@/features/document-authoring/hooks/use-document-list';
import { apiErrorMessage } from '@/lib/api-error';
import { familyContactMethodLabel } from '@/lib/family-contact-label';
import { toDateInputValue } from '@/lib/format-date';
import { recordOfflineInput } from '../api/family-contact-api';
import { FAMILY_CONTACT_METHODS } from '../types';
import type { FamilyContactMethod, OfflineFamilyInputDto } from '../types';

interface RecordOfflineInputFormProps {
  studentId: number;
  onLogged: (input: OfflineFamilyInputDto) => void;
  onCancel: () => void;
}

const SUMMARY_MAX_LENGTH = 4000;

/** Record family input received outside the app: method, date, a summary, and
 *  an optional link to the draft it pertains to (plan 7, decision 7). */
export function RecordOfflineInputForm({ studentId, onLogged, onCancel }: RecordOfflineInputFormProps) {
  const { t } = useTranslation(['family-contact', 'common']);
  const { documents } = useDocumentList(studentId);
  const [method, setMethod] = useState<FamilyContactMethod>('Letter');
  const [receivedAt, setReceivedAt] = useState(toDateInputValue(new Date().toISOString()));
  const [summary, setSummary] = useState('');
  const [documentInstanceId, setDocumentInstanceId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = summary.trim().length > 0 && !isMarkdownOverLimit(summary, SUMMARY_MAX_LENGTH);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await recordOfflineInput(studentId, {
        receivedAt: receivedAt || undefined,
        method,
        summary: summary.trim(),
        documentInstanceId: documentInstanceId ? Number(documentInstanceId) : undefined,
      });
      if (res.success && res.data) {
        onLogged(res.data);
      } else {
        setError(res.message ?? t('offlineForm.saveFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('offlineForm.saveFailed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3" data-testid="record-offline-input-form">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-2">
        <Select
          label={t('offlineForm.methodLabel')}
          value={method}
          onChange={(e) => setMethod(e.target.value as FamilyContactMethod)}
          data-testid="offline-input-method"
        >
          {FAMILY_CONTACT_METHODS.map((m) => (
            <option key={m} value={m}>
              {familyContactMethodLabel(m)}
            </option>
          ))}
        </Select>
        <Input
          label={t('offlineForm.receivedLabel')}
          type="date"
          value={receivedAt}
          onChange={(e) => setReceivedAt(e.target.value)}
          data-testid="offline-input-date"
        />
      </div>
      {documents.length > 0 && (
        <Select
          label={t('offlineForm.linkedDraftLabel')}
          value={documentInstanceId}
          onChange={(e) => setDocumentInstanceId(e.target.value)}
          data-testid="offline-input-document"
        >
          <option value="">{t('offlineForm.noneOption')}</option>
          {documents.map((d) => (
            <option key={d.id} value={d.id}>
              {d.documentTypeDisplayName}
            </option>
          ))}
        </Select>
      )}
      <RichTextEditor
        label={t('offlineForm.summaryLabel')}
        value={summary}
        onChange={setSummary}
        minRows={3}
        maxLength={SUMMARY_MAX_LENGTH}
        required
        data-testid="offline-input-summary"
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={isSubmitting}>
          {t('common:ui.cancel')}
        </Button>
        <Button type="submit" size="sm" loading={isSubmitting} disabled={!canSubmit} data-testid="offline-input-submit">
          {t('offlineForm.submitButton')}
        </Button>
      </div>
    </form>
  );
}
