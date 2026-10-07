import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Markdown } from '@/components/ui/markdown';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { draftResponseKindLabel } from '@/features/shared-drafts/lib/draft-response-kind-label';
import { resolveResponse } from '../api/draft-sharing-api';
import type { DraftResponseDto } from '../types';

interface ResolveResponseDialogProps {
  open: boolean;
  onClose: () => void;
  response: DraftResponseDto | null;
  onResolved: (updated: DraftResponseDto) => void;
}

const MAX_REPLY_LENGTH = 2000;

/** Reply and/or mark a family response resolved. The server requires at least
 *  one of a reply or "resolved in the draft" — enforced client-side too so the
 *  submit button can't fire an empty resolve. */
export function ResolveResponseDialog({ open, onClose, response, onResolved }: ResolveResponseDialogProps) {
  const { t } = useTranslation(['draft-sharing', 'common']);
  const [reply, setReply] = useState('');
  const [resolvedInDraft, setResolvedInDraft] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Modal only unmounts ITS children on close, not this component, so the
  // form fields need their own reset. Computed during render (not an effect)
  // — see `use-shared-draft-detail.ts` for the same idiom — keyed on the
  // transition to open (and on which response, in case it changes while open).
  const openKey = open ? `${response?.id ?? 'none'}` : null;
  const [seenOpenKey, setSeenOpenKey] = useState<string | null>(null);
  if (openKey !== null && openKey !== seenOpenKey) {
    setSeenOpenKey(openKey);
    setReply('');
    setResolvedInDraft(false);
    setError(null);
  }

  if (!response) return null;

  const canSubmit = (reply.trim().length > 0 || resolvedInDraft) && !isMarkdownOverLimit(reply, MAX_REPLY_LENGTH);

  // Click-triggered (never a mount effect), so translating inline here is
  // safe — see `AcknowledgeControl` for the same reasoning.
  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await resolveResponse(response.id, {
        staffReply: reply.trim() || undefined,
        resolvedInDraft,
      });
      if (res.success && res.data) {
        onResolved(res.data);
        onClose();
      } else {
        setError(res.message || t('resolveDialog.submitErrorDefault'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('resolveDialog.submitErrorDefault')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} preventClose={isSubmitting} title={t('resolveDialog.title')} data-testid="resolve-response-dialog">
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="rounded-card border border-brand-slate-200 p-3 text-sm text-brand-slate-600">
          <p className="font-medium text-brand-slate-800">
            {t('resolveDialog.quotedPrefix', { name: response.parentName, kind: draftResponseKindLabel(response.kind) })}
          </p>
          <Markdown content={response.text} className="mt-1" data-testid="resolve-response-quoted-text" />
        </div>

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <RichTextEditor
          label={t('resolveDialog.replyLabel')}
          value={reply}
          onChange={setReply}
          maxLength={MAX_REPLY_LENGTH}
          minRows={4}
          data-testid="resolve-reply-input"
        />

        <label className="flex items-center gap-2 text-sm text-brand-slate-700">
          <input
            type="checkbox"
            checked={resolvedInDraft}
            onChange={(e) => setResolvedInDraft(e.target.checked)}
            data-testid="resolve-in-draft-checkbox"
          />
          {t('resolveDialog.resolvedInDraftLabel')}
        </label>
        {!canSubmit && <p className="text-xs text-brand-slate-500">{t('resolveDialog.needsReplyOrResolved')}</p>}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
            {t('common:ui.cancel')}
          </Button>
          <Button type="submit" loading={isSubmitting} disabled={!canSubmit} data-testid="resolve-response-submit">
            {t('resolveDialog.resolveButton')}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
