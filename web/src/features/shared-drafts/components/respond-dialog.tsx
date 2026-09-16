import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Textarea } from '@/components/ui/input';
import { apiErrorMessage } from '@/lib/api-error';
import { createDraftResponse } from '../api/shared-drafts-api';
import { useDraftReviewContext } from '../hooks/draft-review-context';
import { DRAFT_RESPONSE_KIND_LABELS, type DraftResponseKind } from '../types';

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
  const ctx = useDraftReviewContext();
  const [kind, setKind] = useState<DraftResponseKind>('Agree');
  const [text, setText] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!ctx) return null;

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
        setError(res.message ?? 'Could not send your response.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not send your response.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title={`Respond: ${targetLabel}`} data-testid={testId}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Notice variant="info" title="Sent to the school team">
          Your response is visible to your child's whole school team, not just one person.
        </Notice>

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <fieldset>
          <legend className="mb-1 block text-[13px] font-medium text-brand-slate-600">Response type</legend>
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
                {DRAFT_RESPONSE_KIND_LABELS[k]}
              </label>
            ))}
          </div>
        </fieldset>

        <Textarea
          label="Message"
          value={text}
          onChange={(e) => setText(e.target.value)}
          maxLength={MAX_TEXT_LENGTH}
          rows={4}
          required
          data-testid={`${testId}-text`}
        />

        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" loading={isSubmitting} disabled={!text.trim()} data-testid={`${testId}-submit`}>
            Send
          </Button>
        </div>
      </form>
    </Modal>
  );
}
