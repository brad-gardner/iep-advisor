import { useToast } from '@/components/ui/toast';
import { useEditorContext } from '../hooks/use-editor-context';
import { AddRowButton } from './add-row-button';
import { EmptyHint } from './empty-hint';
import { ServiceLineRow } from './service-line-row';

export function ServicesTable() {
  const { draftApi } = useEditorContext();
  const { show } = useToast();
  const services = draftApi.draft?.serviceLines ?? [];

  const handleDelete = async (id: number) => {
    if (!confirm('Delete this service? This cannot be undone.')) return;
    if (await draftApi.removeServiceLine(id)) {
      show({ message: 'Service deleted', variant: 'success' });
    }
  };

  return (
    <section className="space-y-4" data-testid="services-table">
      {services.length === 0 && <EmptyHint>No services yet. Add one to get started.</EmptyHint>}
      {services.map((service) => (
        <ServiceLineRow key={service.id} service={service} onDelete={handleDelete} />
      ))}
      <AddRowButton
        onClick={() => void draftApi.addServiceLine()}
        label="Add service"
        testId="add-service"
      />
    </section>
  );
}
