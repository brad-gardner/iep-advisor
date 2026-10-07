import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { buildFieldLocationLookup } from '@/features/draft-sharing/lib/field-lookup';
import type { MeetingDecisionOutcome, ProposedEditDto } from '@/features/meetings/types';
import { formatDate } from '@/lib/format-date';
import { meetingDecisionOutcomeLabel } from '@/lib/meeting-labels';
import { useProposedEdits } from '../hooks/use-proposed-edits';
import { jumpToFieldWhenVisible } from '../lib/section-dom';
import { fieldElementId } from './field-renderers/types';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';

const OUTCOME_VARIANT: Record<MeetingDecisionOutcome, 'success' | 'error' | 'warning'> = {
  Agreed: 'success',
  Disagreed: 'error',
  Deferred: 'warning',
};

interface ProposedEditsPanelProps {
  instanceId: number;
  templateVersion: TemplateVersionDetailDto;
}

/**
 * Decisions recorded on meetings linked to this draft (or the same student's
 * recent meetings), surfaced for the editor to act on (plan 7, decision 3).
 * Collapsible so it doesn't compete with the sections when there's nothing new;
 * never edits a field itself — "Jump to field" scrolls/focuses it and "Mark
 * applied" only records that a human already made the change by hand.
 */
export function ProposedEditsPanel({ instanceId, templateVersion }: ProposedEditsPanelProps) {
  // `meetings-staff` alongside `document-authoring`: `meetingDecisionOutcomeLabel`
  // below is backed by that staff-only namespace, and this hook call is what
  // makes a language switch re-render this panel once its Spanish loads.
  const { t } = useTranslation(['document-authoring', 'meetings-staff']);
  const { edits, isLoading, error, retry, markApplied, markingId } = useProposedEdits(instanceId);
  const fieldLookup = useMemo(() => buildFieldLocationLookup(templateVersion), [templateVersion]);

  const jumpTo = (edit: ProposedEditDto) => {
    const loc = edit.targetFieldKey ? fieldLookup.get(edit.targetFieldKey) : undefined;
    if (!loc) return;
    jumpToFieldWhenVisible(fieldElementId(loc.fieldId), loc.sectionId);
  };

  return (
    <details className="rounded-card border border-brand-slate-200 p-4" data-testid="proposed-edits-panel">
      <summary className="cursor-pointer text-sm font-medium text-brand-slate-800">
        {edits.length > 0 ? t('proposedEditsPanel.summaryCount', { count: edits.length }) : t('proposedEditsPanel.summary')}
      </summary>

      <div className="mt-3 space-y-3">
        {error && (
          <div role="alert">
            <Notice
              variant="error"
              title={error.kind === 'server' ? error.message : t('proposedEditsPanel.loadErrorGeneric')}
            >
              <Button size="sm" variant="secondary" className="mt-2" onClick={retry} data-testid="proposed-edits-retry">
                {t('proposedEditsPanel.tryAgain')}
              </Button>
            </Notice>
          </div>
        )}

        {!error && isLoading && <p className="text-sm text-brand-slate-500">{t('proposedEditsPanel.loading')}</p>}

        {!error && !isLoading && edits.length === 0 && (
          <p className="text-sm text-brand-slate-500" data-testid="proposed-edits-empty">
            {t('proposedEditsPanel.empty')}
          </p>
        )}

        {!error && !isLoading && edits.length > 0 && (
          <ul className="space-y-2">
            {edits.map((edit) => (
              <li
                key={edit.decisionId}
                className="rounded-card border border-brand-slate-100 p-3"
                data-testid={`proposed-edit-${edit.decisionId}`}
              >
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <p className="text-xs text-brand-slate-500">
                      {edit.meetingTitle} · {formatDate(edit.recordedAt)}
                    </p>
                    {edit.targetLabel && (
                      <p className="text-xs font-medium text-brand-slate-600">{edit.targetLabel}</p>
                    )}
                    <p className="text-sm text-brand-slate-800">{edit.text}</p>
                  </div>
                  <Badge variant={OUTCOME_VARIANT[edit.outcome]}>{meetingDecisionOutcomeLabel(edit.outcome)}</Badge>
                </div>
                <div className="mt-2 flex items-center gap-2">
                  {edit.targetFieldKey && fieldLookup.has(edit.targetFieldKey) && (
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => jumpTo(edit)}
                      data-testid={`proposed-edit-jump-${edit.decisionId}`}
                    >
                      {t('proposedEditsPanel.jumpToField')}
                    </Button>
                  )}
                  {edit.appliedAt ? (
                    <span className="text-xs text-brand-slate-500" data-testid={`proposed-edit-applied-${edit.decisionId}`}>
                      {t('proposedEditsPanel.applied', { date: formatDate(edit.appliedAt) })}
                    </span>
                  ) : (
                    <Button
                      size="sm"
                      variant="secondary"
                      loading={markingId === edit.decisionId}
                      onClick={() => markApplied(edit.decisionId)}
                      data-testid={`proposed-edit-mark-applied-${edit.decisionId}`}
                    >
                      {t('proposedEditsPanel.markApplied')}
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </details>
  );
}
