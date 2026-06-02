import { useEditorContext } from '../hooks/use-editor-context';
import { AddRowButton } from './add-row-button';
import { GoalEditorRow } from './goal-editor-row';
import { EmptyHint } from './empty-hint';

export function GoalsPanel() {
  const { draftApi } = useEditorContext();
  const goals = draftApi.draft?.goals ?? [];

  const handleDelete = (id: number) => {
    if (confirm('Delete this goal? This cannot be undone.')) {
      void draftApi.removeGoal(id);
    }
  };

  return (
    <section className="space-y-4" data-testid="goals-panel">
      {goals.length === 0 && <EmptyHint>No goals yet. Add one to get started.</EmptyHint>}
      {goals.map((goal) => (
        <GoalEditorRow key={goal.id} goal={goal} onDelete={handleDelete} />
      ))}
      <AddRowButton
        onClick={() => void draftApi.addGoal()}
        label="Add goal"
        testId="add-goal"
      />
    </section>
  );
}
