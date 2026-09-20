import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Notice } from '@/components/ui/notice';
import { Markdown } from '@/components/ui/markdown';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import {
  draftMeetingSummary,
  getMeetingSummary,
  sendMeetingSummary,
  updateMeetingSummary,
} from '@/features/shared-drafts/api/shared-drafts-api';
import type { MeetingSummaryDto } from '@/features/shared-drafts/types';

interface FamilySummaryPanelProps {
  meetingId: number;
}

/** Meeting drawer panel shown once a meeting is Held/Continued: AI-draft a
 *  plain-language family summary, edit it, then send it — an explicit,
 *  reviewable action, never auto-sent. Read-only once Sent. */
export function FamilySummaryPanel({ meetingId }: FamilySummaryPanelProps) {
  const { show: showToast } = useToast();
  // `undefined` = still loading; `null` = loaded, no summary drafted yet.
  const [summary, setSummary] = useState<MeetingSummaryDto | null | undefined>(undefined);
  const [draftText, setDraftText] = useState('');
  const [isDrafting, setIsDrafting] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isSending, setIsSending] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Draft/save/send all write the same summary row — one in-flight operation at a time.
  const isBusy = isDrafting || isSaving || isSending;

  useEffect(() => {
    let active = true;
    getMeetingSummary(meetingId)
      .then((res) => {
        if (!active) return;
        setSummary(res);
        setDraftText(res?.body ?? '');
      })
      .catch((err: unknown) => {
        if (!active) return;
        setError(apiErrorMessage(err, 'Could not load the family summary.'));
        setSummary(null);
      });
    return () => {
      active = false;
    };
  }, [meetingId]);

  const handleDraft = async () => {
    setIsDrafting(true);
    setError(null);
    try {
      const res = await draftMeetingSummary(meetingId);
      if (res.success && res.data) {
        setSummary(res.data);
        setDraftText(res.data.body);
      } else {
        setError(res.message ?? 'Could not draft a summary.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not draft a summary.'));
    } finally {
      setIsDrafting(false);
    }
  };

  const handleSave = async () => {
    setIsSaving(true);
    setError(null);
    try {
      const res = await updateMeetingSummary(meetingId, draftText);
      if (res.success && res.data) {
        setSummary(res.data);
        showToast({ message: 'Draft saved', variant: 'success' });
      } else {
        setError(res.message ?? 'Could not save your edits.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not save your edits.'));
    } finally {
      setIsSaving(false);
    }
  };

  const handleSend = async () => {
    setIsSending(true);
    setError(null);
    try {
      // Persist the latest edit first so Send never ships stale text.
      const saveRes = await updateMeetingSummary(meetingId, draftText);
      if (!saveRes.success || !saveRes.data) {
        setError(saveRes.message ?? 'Could not save your edits.');
        return;
      }
      const sendRes = await sendMeetingSummary(meetingId);
      if (sendRes.success && sendRes.data) {
        setSummary(sendRes.data);
        setConfirmOpen(false);
        showToast({ message: 'Summary sent to the family', variant: 'success' });
      } else {
        setError(sendRes.message ?? 'Could not send the summary.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not send the summary.'));
    } finally {
      setIsSending(false);
    }
  };

  if (summary === undefined) {
    return (
      <div className="flex justify-center py-4">
        <Spinner label="Loading family summary…" size="sm" />
      </div>
    );
  }

  return (
    <div className="space-y-3" data-testid="family-summary-panel">
      <h3 className="text-sm font-medium text-brand-slate-800">Family summary</h3>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      {!summary && (
        <Button size="sm" variant="secondary" onClick={handleDraft} loading={isDrafting} disabled={isBusy} data-testid="family-summary-draft">
          Draft with AI
        </Button>
      )}

      {summary && summary.status === 'Draft' && (
        <div className="space-y-2">
          <RichTextEditor
            label="Summary for the family"
            value={draftText}
            onChange={setDraftText}
            minRows={6}
            data-testid="family-summary-textarea"
          />
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              variant="secondary"
              onClick={handleSave}
              loading={isSaving}
              disabled={isBusy}
              data-testid="family-summary-save"
            >
              Save changes
            </Button>
            <Button
              size="sm"
              onClick={() => setConfirmOpen(true)}
              disabled={!draftText.trim() || isBusy}
              data-testid="family-summary-send-open"
            >
              Send to family
            </Button>
          </div>
        </div>
      )}

      {summary && summary.status === 'Sent' && (
        <div className="space-y-1" data-testid="family-summary-sent">
          <Markdown
            content={summary.body}
            className="rounded-card border border-brand-slate-200 bg-brand-slate-50 p-3"
            data-testid="family-summary-sent-body"
          />
          <p className="text-xs text-brand-slate-500">
            Sent {summary.sentAt ? formatDate(summary.sentAt) : ''}
            {summary.sentByName ? ` by ${summary.sentByName}` : ''}
          </p>
        </div>
      )}

      <ConfirmDialog
        open={confirmOpen}
        title="Send family summary"
        message={
          // ConfirmDialog renders `message` inside a <p>, so this stays
          // phrasing content — no block-level <p>/<ul>/<li> nested inside it.
          <>
            This will send the summary above to:
            {(summary?.recipients ?? []).map((r, i) => (
              <span key={i} className="mt-1 block">
                {r.displayName} ({r.email})
              </span>
            ))}
          </>
        }
        confirmLabel="Send"
        confirmVariant="primary"
        loading={isSending}
        onConfirm={handleSend}
        onCancel={() => setConfirmOpen(false)}
        data-testid="family-summary-send-confirm"
      />
    </div>
  );
}
