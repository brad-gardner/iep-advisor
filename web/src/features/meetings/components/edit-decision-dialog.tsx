import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Select, Textarea } from '@/components/ui/input';
import { apiErrorMessage } from '@/lib/api-error';
import { updateDecision } from '../api/meeting-decisions-api';
import { MEETING_DECISION_OUTCOMES, MEETING_DECISION_OUTCOME_LABELS } from '../types';
import type { MeetingDecisionDto, MeetingDecisionOutcome } from '../types';

interface EditDecisionDialogProps {
  decision: MeetingDecisionDto | null;
  onClose: () => void;
  onUpdated: (decision: MeetingDecisionDto) => void;
}

/** Edit a decision's text/outcome (the target is fixed once recorded). */
export function EditDecisionDialog({ decision, onClose, onUpdated }: EditDecisionDialogProps) {
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
    if (!decision || text.trim().length === 0) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await updateDecision(decision.id, { text: text.trim(), outcome });
      if (res.success && res.data) {
        onUpdated(res.data);
        onClose();
      } else {
        setError(res.message ?? 'Could not update this decision.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not update this decision.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={decision !== null}
      onClose={onClose}
      preventClose={isSubmitting}
      title="Edit decision"
      data-testid="edit-decision-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={text.trim().length === 0}
            data-testid="edit-decision-submit"
          >
            Save changes
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
        <Textarea
          label="Decision *"
          value={text}
          onChange={(e) => setText(e.target.value)}
          rows={3}
          maxLength={2000}
          required
          data-testid="edit-decision-text"
        />
        <Select
          label="Outcome *"
          value={outcome}
          onChange={(e) => setOutcome(e.target.value as MeetingDecisionOutcome)}
          data-testid="edit-decision-outcome"
        >
          {MEETING_DECISION_OUTCOMES.map((o) => (
            <option key={o} value={o}>
              {MEETING_DECISION_OUTCOME_LABELS[o]}
            </option>
          ))}
        </Select>
      </div>
    </Modal>
  );
}
