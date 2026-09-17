import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
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
  const [preview, setPreview] = useState<RecipientPreviewDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
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
        else setLoadError(res.message ?? 'Could not load recipients.');
      } catch (err) {
        if (active) setLoadError(apiErrorMessage(err, 'Could not load recipients.'));
      }
    })();
    return () => {
      active = false;
    };
  }, [open, instanceId]);

  const handleShare = async () => {
    setIsSharing(true);
    setShareError(null);
    try {
      const res = await shareDraft(instanceId, { message: message.trim() || undefined });
      if (res.success && res.data) {
        onShared(res.data);
      } else {
        setShareError(res.message ?? 'Could not share this draft.');
      }
    } catch (err) {
      setShareError(apiErrorMessage(err, 'Could not share this draft.'));
    } finally {
      setIsSharing(false);
    }
  };

  const canShare = Boolean(preview) && preview!.policyEnabled && preview!.recipients.length > 0;

  return (
    <Modal open={open} onClose={onClose} preventClose={isSharing} title="Share with family" data-testid="share-with-family-modal">
      <div className="space-y-4">
        {loadError && (
          <div role="alert">
            <Notice variant="error" title={loadError} />
          </div>
        )}

        {!preview && !loadError && (
          <div className="flex justify-center py-6">
            <Spinner label="Loading recipients…" />
          </div>
        )}

        {preview && (
          <>
            {!preview.policyEnabled && (
              <Notice variant="warning" title="Family draft sharing is disabled" data-testid="share-policy-notice">
                This district has turned off family draft sharing. Ask a district admin to enable it before
                sharing.
              </Notice>
            )}

            <div>
              <p className="mb-2 text-[13px] font-medium text-brand-slate-600">Will be shared with</p>
              {preview.recipients.length === 0 ? (
                <p className="text-sm text-brand-slate-400">No linked family or student accounts yet.</p>
              ) : (
                <ul className="space-y-1.5" data-testid="share-recipient-list">
                  {preview.recipients.map((r) => (
                    <li key={r.userId} className="flex flex-wrap items-center justify-between gap-2 text-sm">
                      <span className="text-brand-slate-800">
                        {r.displayName} <span className="text-brand-slate-400">({r.relationship})</span>
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
                title={`This will supersede revision ${preview.willSupersedeRevision}`}
                data-testid="share-supersede-notice"
              >
                {preview.lastSharedAt ? `Last shared ${formatDate(preview.lastSharedAt)}. ` : ''}
                The family will see this as the newest revision, with what changed highlighted.
              </Notice>
            )}

            <RichTextEditor
              label="Note to the family (optional)"
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
                Cancel
              </Button>
              <Button onClick={handleShare} loading={isSharing} disabled={!canShare} data-testid="share-with-family-submit">
                Share
              </Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}
