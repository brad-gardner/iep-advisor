import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select, Textarea } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { createDecision } from '../api/meeting-decisions-api';
import { MEETING_DECISION_OUTCOMES, MEETING_DECISION_OUTCOME_LABELS } from '../types';
import type { CreateMeetingDecisionRequest, MeetingDecisionDto, MeetingDecisionOutcome } from '../types';
import type { DecisionTargetOption } from '../lib/decision-targets';

const NO_TARGET = '';
const CUSTOM_TARGET = '__custom__';

interface DecisionFormProps {
  meetingId: number;
  /** Rows from the linked draft's goals/services fields; empty when there is
   *  no linked draft (the picker then only offers free-text or no target). */
  targetOptions: DecisionTargetOption[];
  onAdded: (decision: MeetingDecisionDto) => void;
  onCancel: () => void;
}

/** Add a structured decision: an optional target (a goals/services row from
 *  the linked draft, or a free-text label), the text, and an outcome. Never
 *  applies to the draft itself — that's a separate, human "Mark applied" step
 *  from the editor's proposed-edits panel. */
export function DecisionForm({ meetingId, targetOptions, onAdded, onCancel }: DecisionFormProps) {
  const [targetKey, setTargetKey] = useState(NO_TARGET);
  const [customLabel, setCustomLabel] = useState('');
  const [text, setText] = useState('');
  const [outcome, setOutcome] = useState<MeetingDecisionOutcome>('Agreed');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = text.trim().length > 0 && (targetKey !== CUSTOM_TARGET || customLabel.trim().length > 0);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setIsSubmitting(true);
    setError(null);
    const request: CreateMeetingDecisionRequest = { text: text.trim(), outcome };
    if (targetKey === CUSTOM_TARGET) {
      request.targetLabel = customLabel.trim();
    } else if (targetKey !== NO_TARGET) {
      const [fieldKey, rowId] = targetKey.split('::');
      request.targetFieldKey = fieldKey;
      request.targetRowId = rowId;
      request.targetLabel = targetOptions.find((o) => o.fieldKey === fieldKey && o.rowId === rowId)?.label;
    }
    try {
      const res = await createDecision(meetingId, request);
      if (res.success && res.data) {
        onAdded(res.data);
      } else {
        setError(res.message ?? 'Could not record this decision.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not record this decision.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3" data-testid="decision-form">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Select
        label="Target (optional)"
        value={targetKey}
        onChange={(e) => setTargetKey(e.target.value)}
        data-testid="decision-target-select"
      >
        <option value={NO_TARGET}>No specific target</option>
        {targetOptions.map((o) => (
          <option key={`${o.fieldKey}::${o.rowId}`} value={`${o.fieldKey}::${o.rowId}`}>
            {o.label}
          </option>
        ))}
        <option value={CUSTOM_TARGET}>Other — type a label</option>
      </Select>

      {targetKey === CUSTOM_TARGET && (
        <Input
          label="Target label *"
          value={customLabel}
          onChange={(e) => setCustomLabel(e.target.value)}
          maxLength={500}
          data-testid="decision-target-custom"
        />
      )}

      <Textarea
        label="Decision *"
        value={text}
        onChange={(e) => setText(e.target.value)}
        rows={3}
        maxLength={2000}
        required
        data-testid="decision-text"
      />

      <Select
        label="Outcome *"
        value={outcome}
        onChange={(e) => setOutcome(e.target.value as MeetingDecisionOutcome)}
        data-testid="decision-outcome"
      >
        {MEETING_DECISION_OUTCOMES.map((o) => (
          <option key={o} value={o}>
            {MEETING_DECISION_OUTCOME_LABELS[o]}
          </option>
        ))}
      </Select>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" size="sm" loading={isSubmitting} disabled={!canSubmit} data-testid="decision-submit">
          Save decision
        </Button>
      </div>
    </form>
  );
}
