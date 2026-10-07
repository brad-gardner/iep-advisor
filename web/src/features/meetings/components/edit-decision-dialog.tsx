import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Select } from '@/components/ui/input';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { meetingDecisionOutcomeLabel } from '@/lib/meeting-labels';
import { updateDecision } from '../api/meeting-decisions-api';
import { MEETING_DECISION_OUTCOMES } from '../types';
import type { MeetingDecisionDto, MeetingDecisionOutcome } from '../types';

interface EditDecisionDialogProps {
  decision: MeetingDecisionDto | null;
  onClose: () => void;
  onUpdated: (decision: MeetingDecisionDto) => void;
}

const TEXT_MAX_LENGTH = 2000;

/** Edit a decision's text/outcome (the target is fixed once recorded). */
export function EditDecisionDialog({ decision, onClose, onUpdated }: EditDecisionDialogProps) {
  const { t } = useTranslation(['meetings-staff', 'common']);
  const [text, setText] = useState(decision?.text ?? '');
  const [outcome, setOutcome] = useState<MeetingDecisionOutcome>(decision?.outcome ?? 'Agreed');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [seenId, setSeenId] = useState<number | null>(decision?.id ?? null);

  // Reseed local state whenever a different decision is opened (computed
  // during render, matching the meeting drawer's own per-selection reset idiom).
  if (decision && decision.id !== seenId) {
    setSeenId(decision.id);
    setText(decision.text);
    setOutcome(decision.outcome);
    setError(null);
  }

  const handleSubmit = async () => {
    if (!decision || text.trim().length === 0 || isMarkdownOverLimit(text, TEXT_MAX_LENGTH)) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await updateDecision(decision.id, { text: text.trim(), outcome });
      if (res.success && res.data) {
        onUpdated(res.data);
        onClose();
      } else {
        setError(res.message ?? t('editDecisionDialog.saveFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('editDecisionDialog.saveFailed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={decision !== null}
      onClose={onClose}
      preventClose={isSubmitting}
      title={t('editDecisionDialog.title')}
      data-testid="edit-decision-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={isSubmitting}>
            {t('common:ui.cancel')}
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={text.trim().length === 0 || isMarkdownOverLimit(text, TEXT_MAX_LENGTH)}
            data-testid="edit-decision-submit"
          >
            {t('editDecisionDialog.saveButton')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}
        <RichTextEditor
          label={t('editDecisionDialog.decisionLabel')}
          value={text}
          onChange={setText}
          minRows={3}
          maxLength={TEXT_MAX_LENGTH}
          required
          data-testid="edit-decision-text"
        />
        <Select
          label={t('editDecisionDialog.outcomeLabel')}
          value={outcome}
          onChange={(e) => setOutcome(e.target.value as MeetingDecisionOutcome)}
          data-testid="edit-decision-outcome"
        >
          {MEETING_DECISION_OUTCOMES.map((o) => (
            <option key={o} value={o}>
              {meetingDecisionOutcomeLabel(o)}
            </option>
          ))}
        </Select>
      </div>
    </Modal>
  );
}
