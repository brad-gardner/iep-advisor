import { Card } from '@/components/ui/card';
import { Input, Textarea } from '@/components/ui/input';
import { useEditorContext } from '../hooks/use-editor-context';
import { useRowAutosave } from '../hooks/use-row-autosave';
import type { AccommodationDto } from '../types';
import { LastEditedStamp } from './last-edited-stamp';
import { RowDeleteButton } from './row-delete-button';

interface AccommodationRowProps {
  accommodation: AccommodationDto;
  onDelete: (id: number) => void;
}

export function AccommodationRow({ accommodation, onDelete }: AccommodationRowProps) {
  const { draftApi, bus, registry, currentUserId } = useEditorContext();
  const { schedule, cancel } = useRowAutosave({
    rowKey: `accommodation-${accommodation.id}`,
    save: () => draftApi.saveAccommodation(accommodation.id),
    bus,
    registry,
  });

  const edit = (patch: Partial<AccommodationDto>) => {
    draftApi.patchAccommodation(accommodation.id, patch);
    schedule();
  };

  // Cancel any pending debounced save first so the unmount-flush is a no-op and
  // no PUT races the DELETE against a now-deleted id.
  const handleDelete = async () => {
    cancel();
    await onDelete(accommodation.id);
  };

  return (
    <Card className="space-y-3" data-testid={`accommodation-row-${accommodation.id}`}>
      <Input
        label="Category"
        value={accommodation.category ?? ''}
        onChange={(e) => edit({ category: e.target.value })}
        data-testid={`accommodation-category-${accommodation.id}`}
      />
      <Textarea
        label="Accommodation"
        rows={2}
        value={accommodation.text ?? ''}
        onChange={(e) => edit({ text: e.target.value })}
        data-testid={`accommodation-text-${accommodation.id}`}
      />
      <div className="flex items-center justify-between pt-1">
        <LastEditedStamp
          lastEditedAt={accommodation.lastEditedAt}
          lastEditedByUserId={accommodation.lastEditedByUserId}
          currentUserId={currentUserId}
        />
        <RowDeleteButton
          onDelete={() => void handleDelete()}
          label="Delete accommodation"
          testId={`accommodation-delete-${accommodation.id}`}
        />
      </div>
    </Card>
  );
}
