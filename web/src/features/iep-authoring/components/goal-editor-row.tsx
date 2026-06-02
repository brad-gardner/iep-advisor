import { useCallback } from 'react';
import { Card } from '@/components/ui/card';
import { Input, Textarea } from '@/components/ui/input';
import { assistGoal } from '../api/iep-assist-api';
import type { AssistKind } from '../api/iep-assist-types';
import { useEditorContext } from '../hooks/use-editor-context';
import { useRowAutosave } from '../hooks/use-row-autosave';
import type { GoalDto } from '../types';
import { AssistPopover } from './assist/assist-popover';
import { PullFromStudentButton } from './pull-from-student/pull-from-student-button';
import { LastEditedStamp } from './last-edited-stamp';
import { RowDeleteButton } from './row-delete-button';

interface GoalEditorRowProps {
  goal: GoalDto;
  onDelete: (id: number) => void;
}

export function GoalEditorRow({ goal, onDelete }: GoalEditorRowProps) {
  const { draftId, draftApi, bus, registry, currentUserId } = useEditorContext();
  const { schedule, cancel } = useRowAutosave({
    rowKey: `goal-${goal.id}`,
    save: () => draftApi.saveGoal(goal.id),
    bus,
    registry,
  });

  const edit = (patch: Partial<GoalDto>) => {
    draftApi.patchGoal(goal.id, patch);
    schedule();
  };

  const assistRequest = useCallback(
    (kind: AssistKind) => assistGoal(draftId, goal.id, kind),
    [draftId, goal.id]
  );

  // Cancel any pending debounced save first so the unmount-flush is a no-op and
  // no PUT races the DELETE against a now-deleted id.
  const handleDelete = async () => {
    cancel();
    await onDelete(goal.id);
  };

  return (
    <Card className="space-y-3" data-testid={`goal-row-${goal.id}`}>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Input
          label="Domain"
          value={goal.domain ?? ''}
          onChange={(e) => edit({ domain: e.target.value })}
          data-testid={`goal-domain-${goal.id}`}
        />
        <Input
          label="Timeframe"
          value={goal.timeframe ?? ''}
          onChange={(e) => edit({ timeframe: e.target.value })}
          data-testid={`goal-timeframe-${goal.id}`}
        />
      </div>
      <div className="space-y-2">
        <Textarea
          label="Goal"
          rows={3}
          value={goal.goalText ?? ''}
          onChange={(e) => edit({ goalText: e.target.value })}
          data-testid={`goal-text-${goal.id}`}
        />
        <div className="flex flex-wrap items-start gap-2">
          <AssistPopover
            requestFn={assistRequest}
            onApply={(text) => edit({ goalText: text })}
            testIdPrefix={`goal-assist-${goal.id}`}
          />
          <PullFromStudentButton
            onPick={(content) => edit({ goalText: content })}
            testIdPrefix={`goal-pull-${goal.id}`}
          />
        </div>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Input
          label="Baseline"
          value={goal.baseline ?? ''}
          onChange={(e) => edit({ baseline: e.target.value })}
          data-testid={`goal-baseline-${goal.id}`}
        />
        <Input
          label="Target criteria"
          value={goal.targetCriteria ?? ''}
          onChange={(e) => edit({ targetCriteria: e.target.value })}
          data-testid={`goal-target-${goal.id}`}
        />
      </div>
      <Input
        label="Measurement method"
        value={goal.measurementMethod ?? ''}
        onChange={(e) => edit({ measurementMethod: e.target.value })}
        data-testid={`goal-measurement-${goal.id}`}
      />
      <div className="flex items-center justify-between pt-1">
        <LastEditedStamp
          lastEditedAt={goal.lastEditedAt}
          lastEditedByUserId={goal.lastEditedByUserId}
          currentUserId={currentUserId}
        />
        <RowDeleteButton
          onDelete={() => void handleDelete()}
          label="Delete goal"
          testId={`goal-delete-${goal.id}`}
        />
      </div>
    </Card>
  );
}
