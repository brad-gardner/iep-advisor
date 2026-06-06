import { useEditorContext } from '../hooks/use-editor-context';
import { AccommodationRow } from './accommodation-row';
import { AddRowButton } from './add-row-button';
import { EmptyHint } from './empty-hint';

export function AccommodationsList() {
  const { draftApi } = useEditorContext();
  const accommodations = draftApi.draft?.accommodations ?? [];

  const handleDelete = (id: number) => {
    if (confirm('Delete this accommodation? This cannot be undone.')) {
      void draftApi.removeAccommodation(id);
    }
  };

  return (
    <section className="space-y-4" data-testid="accommodations-list">
      {accommodations.length === 0 && (
        <EmptyHint>No accommodations yet. Add one to get started.</EmptyHint>
      )}
      {accommodations.map((accommodation) => (
        <AccommodationRow
          key={accommodation.id}
          accommodation={accommodation}
          onDelete={handleDelete}
        />
      ))}
      <AddRowButton
        onClick={() => void draftApi.addAccommodation()}
        label="Add accommodation"
        testId="add-accommodation"
      />
    </section>
  );
}
