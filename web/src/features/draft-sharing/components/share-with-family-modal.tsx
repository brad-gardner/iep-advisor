import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { Spinner } from '@/components/ui/spinner';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { getSharePreview, shareDraft } from '../api/draft-sharing-api';
import type { RecipientPreviewDto, SharedDraftRevisionDto } from '../types';

interface ShareWithFamilyModalProps {
  open: boolean;
  onClose: () => void;
  instanceId: number;
  onShared: (revision: SharedDraftRevisionDto) => void;
}

const MAX_MESSAGE_LENGTH = 1000;

/** Deliberate share action: shows exactly who will receive the draft (and
 *  whether this would supersede a prior revision) before the irreversible
 *  snapshot happens. Fetches a fresh preview every time it opens. */
export function ShareWithFamilyModal({ open, onClose, instanceId, onShared }: ShareWithFamilyModalProps) {
  const { t } = useTranslation(['draft-sharing', 'common']);
  const [preview, setPreview] = useState<RecipientPreviewDto | null>(null);
  // A server-provided message is already resolved text; the generic fallback
  // is translated at RENDER time below, from the stored KIND, so the mount
  // effect never needs `t` in its dependency array (same idiom as `useHome`).
  const [loadError, setLoadError] = useState<{ kind: 'server'; message: string } | { kind: 'generic' } | null>(null);
  const [message, setMessage] = useState('');
  const [isSharing, setIsSharing] = useState(false);
  const [shareError, setShareError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let active = true;
    (async () => {
      setPreview(null);
      setLoadError(null);
      setMessage('');
      setShareError(null);
      try {
        const res = await getSharePreview(instanceId);
        if (!active) return;
        if (res.success && res.data) setPreview(res.data);
        else setLoadError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      } catch (err) {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setLoadError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see `loadError`'s own comment above.
  }, [open, instanceId]);

  // Click-triggered (never a mount effect), so translating inline here is
  // safe — see `AcknowledgeControl` for the same reasoning.
  const handleShare = async () => {
    setIsSharing(true);
    setShareError(null);
    try {
      const res = await shareDraft(instanceId, { message: message.trim() || undefined });
      if (res.success && res.data) {
        onShared(res.data);
      } else {
        setShareError(res.message || t('shareModal.shareErrorDefault'));
      }
    } catch (err) {
      setShareError(apiErrorMessage(err, t('shareModal.shareErrorDefault')));
    } finally {
      setIsSharing(false);
    }
  };

  const canShare =
    Boolean(preview) &&
    preview!.policyEnabled &&
    preview!.recipients.length > 0 &&
    !isMarkdownOverLimit(message, MAX_MESSAGE_LENGTH);

  return (
    <Modal open={open} onClose={onClose} preventClose={isSharing} title={t('shareWithFamilyLabel')} data-testid="share-with-family-modal">
      <div className="space-y-4">
        {loadError && (
          <div role="alert">
            <Notice variant="error" title={loadError.kind === 'server' ? loadError.message : t('shareModal.loadErrorDefault')} />
          </div>
        )}

        {!preview && !loadError && (
          <div className="flex justify-center py-6">
            <Spinner label={t('shareModal.loadingRecipients')} />
          </div>
        )}

        {preview && (
          <>
            {!preview.policyEnabled && (
              <Notice variant="warning" title={t('shareModal.policyDisabledTitle')} data-testid="share-policy-notice">
                {t('shareModal.policyDisabledBody')}
              </Notice>
            )}

            <div>
              <p className="mb-2 text-[13px] font-medium text-brand-slate-600">{t('shareModal.willBeSharedWith')}</p>
              {preview.recipients.length === 0 ? (
                <p className="text-sm text-brand-slate-500">{t('shareModal.noRecipients')}</p>
              ) : (
                <ul className="space-y-1.5" data-testid="share-recipient-list">
                  {preview.recipients.map((r) => (
                    <li key={r.userId} className="flex flex-wrap items-center justify-between gap-2 text-sm">
                      <span className="text-brand-slate-800">
                        {r.displayName} <span className="text-brand-slate-500">({r.relationship})</span>
                      </span>
                      <span className="text-brand-slate-500">{r.email}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            {preview.willSupersedeRevision != null && (
              <Notice
                variant="info"
                title={t('shareModal.supersedeTitle', { number: preview.willSupersedeRevision })}
                data-testid="share-supersede-notice"
              >
                {preview.lastSharedAt ? t('shareModal.lastSharedPrefix', { date: formatDate(preview.lastSharedAt) }) : ''}
                {t('shareModal.supersedeBody')}
              </Notice>
            )}

            <RichTextEditor
              label={t('shareModal.noteLabel')}
              value={message}
              onChange={setMessage}
              maxLength={MAX_MESSAGE_LENGTH}
              minRows={3}
              data-testid="share-message-input"
            />

            {shareError && (
              <div role="alert">
                <Notice variant="error" title={shareError} />
              </div>
            )}

            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={onClose} disabled={isSharing}>
                {t('common:ui.cancel')}
              </Button>
              <Button onClick={handleShare} loading={isSharing} disabled={!canShare} data-testid="share-with-family-submit">
                {t('shareModal.shareButton')}
              </Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}
