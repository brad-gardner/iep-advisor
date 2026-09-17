import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { useDocumentList } from '@/features/document-authoring/hooks/use-document-list';
import { apiErrorMessage } from '@/lib/api-error';
import { toDateInputValue } from '@/lib/format-date';
import { recordOfflineInput } from '../api/family-contact-api';
import { FAMILY_CONTACT_METHODS, FAMILY_CONTACT_METHOD_LABELS } from '../types';
import type { FamilyContactMethod, OfflineFamilyInputDto } from '../types';

interface RecordOfflineInputFormProps {
  studentId: number;
  onLogged: (input: OfflineFamilyInputDto) => void;
  onCancel: () => void;
}

/** Record family input received outside the app: method, date, a summary, and
 *  an optional link to the draft it pertains to (plan 7, decision 7). */
export function RecordOfflineInputForm({ studentId, onLogged, onCancel }: RecordOfflineInputFormProps) {
  const { documents } = useDocumentList(studentId);
  const [method, setMethod] = useState<FamilyContactMethod>('Letter');
  const [receivedAt, setReceivedAt] = useState(toDateInputValue(new Date().toISOString()));
  const [summary, setSummary] = useState('');
  const [documentInstanceId, setDocumentInstanceId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = summary.trim().length > 0;

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
        setError(res.message ?? 'Could not record this input.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not record this input.'));
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
          label="Method *"
          value={method}
          onChange={(e) => setMethod(e.target.value as FamilyContactMethod)}
          data-testid="offline-input-method"
        >
          {FAMILY_CONTACT_METHODS.map((m) => (
            <option key={m} value={m}>
              {FAMILY_CONTACT_METHOD_LABELS[m]}
            </option>
          ))}
        </Select>
        <Input
          label="Received"
          type="date"
          value={receivedAt}
          onChange={(e) => setReceivedAt(e.target.value)}
          data-testid="offline-input-date"
        />
      </div>
      {documents.length > 0 && (
        <Select
          label="Linked draft (optional)"
          value={documentInstanceId}
          onChange={(e) => setDocumentInstanceId(e.target.value)}
          data-testid="offline-input-document"
        >
          <option value="">None</option>
          {documents.map((d) => (
            <option key={d.id} value={d.id}>
              {d.documentTypeDisplayName}
            </option>
          ))}
        </Select>
      )}
      <RichTextEditor
        label="Summary *"
        value={summary}
        onChange={setSummary}
        minRows={3}
        maxLength={4000}
        required
        data-testid="offline-input-summary"
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" size="sm" loading={isSubmitting} disabled={!canSubmit} data-testid="offline-input-submit">
          Record input
        </Button>
      </div>
    </form>
  );
}
