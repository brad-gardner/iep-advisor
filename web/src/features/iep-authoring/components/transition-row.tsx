import { Card } from '@/components/ui/card';
import { Input, Textarea } from '@/components/ui/input';
import { useEditorContext } from '../hooks/use-editor-context';
import { useRowAutosave } from '../hooks/use-row-autosave';
import type { TransitionItemDto } from '../types';
import { LastEditedStamp } from './last-edited-stamp';
import { RowDeleteButton } from './row-delete-button';

interface TransitionRowProps {
  item: TransitionItemDto;
  onDelete: (id: number) => void;
}

export function TransitionRow({ item, onDelete }: TransitionRowProps) {
  const { draftApi, bus, registry, currentUserId } = useEditorContext();
  const { schedule, cancel } = useRowAutosave({
    rowKey: `transition-${item.id}`,
    save: () => draftApi.saveTransitionItem(item.id),
    bus,
    registry,
  });

  const edit = (patch: Partial<TransitionItemDto>) => {
    draftApi.patchTransitionItem(item.id, patch);
    schedule();
  };

  // Cancel any pending debounced save first so the unmount-flush is a no-op and
  // no PUT races the DELETE against a now-deleted id.
  const handleDelete = async () => {
    cancel();
    await onDelete(item.id);
  };

  return (
    <Card className="space-y-3" data-testid={`transition-row-${item.id}`}>
      <Input
        label="Postsecondary goal area"
        value={item.postsecondaryGoalArea ?? ''}
        onChange={(e) => edit({ postsecondaryGoalArea: e.target.value })}
        data-testid={`transition-area-${item.id}`}
      />
      <Textarea
        label="Transition services"
        rows={3}
        value={item.servicesText ?? ''}
        onChange={(e) => edit({ servicesText: e.target.value })}
        data-testid={`transition-services-${item.id}`}
      />
      <div className="flex items-center justify-between pt-1">
        <LastEditedStamp
          lastEditedAt={item.lastEditedAt}
          lastEditedByUserId={item.lastEditedByUserId}
          currentUserId={currentUserId}
        />
        <RowDeleteButton
          onDelete={() => void handleDelete()}
          label="Delete transition item"
          testId={`transition-delete-${item.id}`}
        />
      </div>
    </Card>
  );
}
