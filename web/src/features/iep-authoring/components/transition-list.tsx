import { useToast } from '@/components/ui/toast';
import { useEditorContext } from '../hooks/use-editor-context';
import { AddRowButton } from './add-row-button';
import { EmptyHint } from './empty-hint';
import { TransitionRow } from './transition-row';

export function TransitionList() {
  const { draftApi } = useEditorContext();
  const { show } = useToast();
  const items = draftApi.draft?.transitionItems ?? [];

  const handleDelete = async (id: number) => {
    if (!confirm('Delete this transition item? This cannot be undone.')) return;
    if (await draftApi.removeTransitionItem(id)) {
      show({ message: 'Transition item deleted', variant: 'success' });
    }
  };

  return (
    <section className="space-y-4" data-testid="transition-list">
      {items.length === 0 && (
        <EmptyHint>No transition items yet. Add one to get started.</EmptyHint>
      )}
      {items.map((item) => (
        <TransitionRow key={item.id} item={item} onDelete={handleDelete} />
      ))}
      <AddRowButton
        onClick={() => void draftApi.addTransitionItem()}
        label="Add transition item"
        testId="add-transition"
      />
    </section>
  );
}
