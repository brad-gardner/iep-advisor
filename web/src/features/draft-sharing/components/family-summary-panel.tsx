import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Notice } from '@/components/ui/notice';
import { Markdown } from '@/components/ui/markdown';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';
import { GeneratedLanguageNotice } from '@/lib/i18n/generated-language-notice';
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
  const { t } = useTranslation('draft-sharing');
  const { show: showToast } = useToast();
  // `undefined` = still loading; `null` = loaded, no summary drafted yet.
  const [summary, setSummary] = useState<MeetingSummaryDto | null | undefined>(undefined);
  const [draftText, setDraftText] = useState('');
  const [isDrafting, setIsDrafting] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isSending, setIsSending] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  // A plain `string` is already-resolved text (a server message, or a
  // handler's own `t(...)` fallback — handlers are click-triggered, so
  // translating inline there is safe, see below). `{ kind: 'generic' }` is
  // the ONE case that isn't: the mount effect's catch below must never call
  // `t` itself (it would need `t` in its dependency array, re-running the
  // fetch on every language switch — see `useHome`), so it stores this flag
  // instead and the render block translates it fresh, every render.
  const [error, setError] = useState<string | { kind: 'generic' } | null>(null);
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
        const serverMessage = apiErrorMessage(err, '');
        setError(serverMessage || { kind: 'generic' });
        setSummary(null);
      });
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see the `error` state's own comment above.
  }, [meetingId]);

  // Every handler below is click-triggered (never a mount effect), so
  // translating inline is safe — see `AcknowledgeControl` for the reasoning.
  const handleDraft = async () => {
    setIsDrafting(true);
    setError(null);
    try {
      const res = await draftMeetingSummary(meetingId);
      if (res.success && res.data) {
        setSummary(res.data);
        setDraftText(res.data.body);
      } else {
        setError(res.message || t('familySummary.draftErrorDefault'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('familySummary.draftErrorDefault')));
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
        showToast({ message: t('familySummary.savedToast'), variant: 'success' });
      } else {
        setError(res.message || t('familySummary.saveErrorDefault'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('familySummary.saveErrorDefault')));
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
        setError(saveRes.message || t('familySummary.saveErrorDefault'));
        return;
      }
      const sendRes = await sendMeetingSummary(meetingId);
      if (sendRes.success && sendRes.data) {
        setSummary(sendRes.data);
        setConfirmOpen(false);
        showToast({ message: t('familySummary.sentToast'), variant: 'success' });
      } else {
        setError(sendRes.message || t('familySummary.sendErrorDefault'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('familySummary.sendErrorDefault')));
    } finally {
      setIsSending(false);
    }
  };

  if (summary === undefined) {
    return (
      <div className="flex justify-center py-4">
        <Spinner label={t('familySummary.loading')} size="sm" />
      </div>
    );
  }

  return (
    <div className="space-y-3" data-testid="family-summary-panel">
      <h3 className="text-sm font-medium text-brand-slate-800">{t('familySummary.heading')}</h3>

      {error && (
        <div role="alert">
          <Notice variant="error" title={typeof error === 'string' ? error : t('familySummary.loadErrorDefault')} />
        </div>
      )}

      {!summary && (
        <Button size="sm" variant="secondary" onClick={handleDraft} loading={isDrafting} disabled={isBusy} data-testid="family-summary-draft">
          {t('familySummary.draftWithAi')}
        </Button>
      )}

      {summary && summary.status === 'Draft' && (
        <div className="space-y-2">
          <RichTextEditor
            label={t('familySummary.summaryLabel')}
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
              {t('familySummary.saveChanges')}
            </Button>
            <Button
              size="sm"
              onClick={() => setConfirmOpen(true)}
              disabled={!draftText.trim() || isBusy}
              data-testid="family-summary-send-open"
            >
              {t('familySummary.sendToFamily')}
            </Button>
          </div>
        </div>
      )}

      {summary && summary.status === 'Sent' && (
        <div className="space-y-1" data-testid="family-summary-sent">
          <GeneratedLanguageNotice generatedLanguage={summary.generatedLanguage} className="mb-2" />
          <Markdown
            content={summary.body}
            className="rounded-card border border-brand-slate-200 bg-brand-slate-50 p-3"
            data-testid="family-summary-sent-body"
          />
          <p className="text-xs text-brand-slate-500">
            {summary.sentAt
              ? summary.sentByName
                ? t('familySummary.sentLineByName', { date: formatDate(summary.sentAt), name: summary.sentByName })
                : t('familySummary.sentLine', { date: formatDate(summary.sentAt) })
              : ''}
          </p>
        </div>
      )}

      <ConfirmDialog
        open={confirmOpen}
        title={t('familySummary.confirmTitle')}
        message={
          // ConfirmDialog renders `message` inside a <p>, so this stays
          // phrasing content — no block-level <p>/<ul>/<li> nested inside it.
          <>
            {t('familySummary.confirmBody')}
            {(summary?.recipients ?? []).map((r, i) => (
              <span key={i} className="mt-1 block">
                {t('familySummary.recipientLine', { name: r.displayName, email: r.email })}
              </span>
            ))}
          </>
        }
        confirmLabel={t('familySummary.confirmSend')}
        confirmVariant="primary"
        loading={isSending}
        onConfirm={handleSend}
        onCancel={() => setConfirmOpen(false)}
        data-testid="family-summary-send-confirm"
      />
    </div>
  );
}
