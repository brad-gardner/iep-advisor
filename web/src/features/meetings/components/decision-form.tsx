import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { meetingDecisionOutcomeLabel } from '@/lib/meeting-labels';
import { createDecision } from '../api/meeting-decisions-api';
import { MEETING_DECISION_OUTCOMES } from '../types';
import type { CreateMeetingDecisionRequest, MeetingDecisionDto, MeetingDecisionOutcome } from '../types';
import type { DecisionTargetOption } from '../lib/decision-targets';

const NO_TARGET = '';
const CUSTOM_TARGET = '__custom__';
const TEXT_MAX_LENGTH = 2000;

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
  const { t } = useTranslation(['meetings-staff', 'common']);
  const [targetKey, setTargetKey] = useState(NO_TARGET);
  const [customLabel, setCustomLabel] = useState('');
  const [text, setText] = useState('');
  const [outcome, setOutcome] = useState<MeetingDecisionOutcome>('Agreed');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit =
    text.trim().length > 0 &&
    (targetKey !== CUSTOM_TARGET || customLabel.trim().length > 0) &&
    !isMarkdownOverLimit(text, TEXT_MAX_LENGTH);

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
        setError(res.message ?? t('decisionForm.saveFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('decisionForm.saveFailed')));
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
        label={t('decisionForm.targetLabel')}
        value={targetKey}
        onChange={(e) => setTargetKey(e.target.value)}
        data-testid="decision-target-select"
      >
        <option value={NO_TARGET}>{t('decisionForm.noSpecificTarget')}</option>
        {targetOptions.map((o) => (
          <option key={`${o.fieldKey}::${o.rowId}`} value={`${o.fieldKey}::${o.rowId}`}>
            {o.label}
          </option>
        ))}
        <option value={CUSTOM_TARGET}>{t('decisionForm.otherTypeLabel')}</option>
      </Select>

      {targetKey === CUSTOM_TARGET && (
        <Input
          label={t('decisionForm.targetLabelInputLabel')}
          value={customLabel}
          onChange={(e) => setCustomLabel(e.target.value)}
          maxLength={500}
          data-testid="decision-target-custom"
        />
      )}

      <RichTextEditor
        label={t('decisionForm.decisionLabel')}
        value={text}
        onChange={setText}
        minRows={3}
        maxLength={TEXT_MAX_LENGTH}
        required
        data-testid="decision-text"
      />

      <Select
        label={t('decisionForm.outcomeLabel')}
        value={outcome}
        onChange={(e) => setOutcome(e.target.value as MeetingDecisionOutcome)}
        data-testid="decision-outcome"
      >
        {MEETING_DECISION_OUTCOMES.map((o) => (
          <option key={o} value={o}>
            {meetingDecisionOutcomeLabel(o)}
          </option>
        ))}
      </Select>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={isSubmitting}>
          {t('common:ui.cancel')}
        </Button>
        <Button type="submit" size="sm" loading={isSubmitting} disabled={!canSubmit} data-testid="decision-submit">
          {t('decisionForm.saveButton')}
        </Button>
      </div>
    </form>
  );
}
