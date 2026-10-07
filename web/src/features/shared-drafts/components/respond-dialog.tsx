import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { createDraftResponse } from '../api/shared-drafts-api';
import { useDraftReviewContext } from '../hooks/draft-review-context';
import { draftResponseKindLabel } from '../lib/draft-response-kind-label';
import type { DraftResponseKind } from '../types';

interface RespondDialogProps {
  open: boolean;
  onClose: () => void;
  revisionId: number;
  targetFieldKey?: string;
  targetRowId?: string;
  targetLabel: string;
  /** Unique per card — two cards render one Modal each, always mounted
   *  (Modal keeps its own `<dialog>` in the DOM even while closed). */
  'data-testid': string;
}

const RESPOND_KINDS: DraftResponseKind[] = ['Agree', 'Question', 'ChangeRequest'];
const MAX_TEXT_LENGTH = 2000;

/** Agree / Question / Request a change on one item — visible to the whole
 *  school team, never just one person, so the dialog says so up front. */
export function RespondDialog({
  open,
  onClose,
  revisionId,
  targetFieldKey,
  targetRowId,
  targetLabel,
  'data-testid': testId,
}: RespondDialogProps) {
  const { t } = useTranslation(['shared-drafts', 'common']);
  const ctx = useDraftReviewContext();
  const [kind, setKind] = useState<DraftResponseKind>('Agree');
  const [text, setText] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!ctx) return null;

  // Click-triggered (never a mount effect), so translating inline here is
  // safe — see `AcknowledgeControl` for the same reasoning.
  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const trimmed = text.trim();
    if (!trimmed) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await createDraftResponse(revisionId, { kind, text: trimmed, targetFieldKey, targetRowId });
      if (res.success && res.data) {
        ctx.addResponse(res.data);
        setKind('Agree');
        setText('');
        onClose();
      } else {
        setError(res.message || t('respondDialog.submitError'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('respondDialog.submitError')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      preventClose={isSubmitting}
      title={t('respondDialog.title', { label: targetLabel })}
      data-testid={testId}
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <Notice variant="info" title={t('respondDialog.sentToSchoolTitle')}>
          {t('respondDialog.sentToSchoolBody')}
        </Notice>

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <fieldset>
          <legend className="mb-1 block text-[13px] font-medium text-brand-slate-600">
            {t('respondDialog.responseTypeLegend')}
          </legend>
          <div className="flex flex-wrap gap-3">
            {RESPOND_KINDS.map((k) => (
              <label key={k} className="inline-flex items-center gap-1.5 text-sm text-brand-slate-700">
                <input
                  type="radio"
                  name={`${testId}-kind`}
                  value={k}
                  checked={kind === k}
                  onChange={() => setKind(k)}
                  data-testid={`${testId}-kind-${k}`}
                />
                {draftResponseKindLabel(k)}
              </label>
            ))}
          </div>
        </fieldset>

        <RichTextEditor
          label={t('respondDialog.messageLabel')}
          value={text}
          onChange={setText}
          maxLength={MAX_TEXT_LENGTH}
          minRows={4}
          required
          data-testid={`${testId}-text`}
        />

        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
            {t('common:ui.cancel')}
          </Button>
          <Button
            type="submit"
            loading={isSubmitting}
            disabled={!text.trim() || isMarkdownOverLimit(text, MAX_TEXT_LENGTH)}
            data-testid={`${testId}-submit`}
          >
            {t('respondDialog.send')}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
