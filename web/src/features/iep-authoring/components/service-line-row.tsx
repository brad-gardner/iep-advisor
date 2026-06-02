import { useCallback } from 'react';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { assistServiceLine } from '../api/iep-assist-api';
import type { AssistKind } from '../api/iep-assist-types';
import { useEditorContext } from '../hooks/use-editor-context';
import { useRowAutosave } from '../hooks/use-row-autosave';
import type { ServiceLineDto } from '../types';
import { AssistPopover } from './assist/assist-popover';
import { LastEditedStamp } from './last-edited-stamp';
import { RowDeleteButton } from './row-delete-button';

interface ServiceLineRowProps {
  service: ServiceLineDto;
  onDelete: (id: number) => void;
}

// Dates arrive as ISO; <input type="date"> wants YYYY-MM-DD.
const toDateInput = (iso: string | null) => (iso ? iso.slice(0, 10) : '');

export function ServiceLineRow({ service, onDelete }: ServiceLineRowProps) {
  const { draftId, draftApi, bus, registry, currentUserId } = useEditorContext();
  const { schedule, cancel } = useRowAutosave({
    rowKey: `service-${service.id}`,
    save: () => draftApi.saveServiceLine(service.id),
    bus,
    registry,
  });

  const edit = (patch: Partial<ServiceLineDto>) => {
    draftApi.patchServiceLine(service.id, patch);
    schedule();
  };

  const assistRequest = useCallback(
    (kind: AssistKind) => assistServiceLine(draftId, service.id, kind),
    [draftId, service.id]
  );

  // Cancel any pending debounced save first so the unmount-flush is a no-op and
  // no PUT races the DELETE against a now-deleted id.
  const handleDelete = async () => {
    cancel();
    await onDelete(service.id);
  };

  return (
    <Card className="space-y-3" data-testid={`service-row-${service.id}`}>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Input
          label="Service type"
          value={service.serviceType ?? ''}
          onChange={(e) => edit({ serviceType: e.target.value })}
          data-testid={`service-type-${service.id}`}
        />
        <Input
          label="Provider role"
          value={service.providerRole ?? ''}
          onChange={(e) => edit({ providerRole: e.target.value })}
          data-testid={`service-provider-${service.id}`}
        />
        <Input
          label="Frequency"
          value={service.frequency ?? ''}
          onChange={(e) => edit({ frequency: e.target.value })}
          data-testid={`service-frequency-${service.id}`}
        />
        <Input
          label="Duration"
          value={service.duration ?? ''}
          onChange={(e) => edit({ duration: e.target.value })}
          data-testid={`service-duration-${service.id}`}
        />
        <Input
          label="Location"
          value={service.location ?? ''}
          onChange={(e) => edit({ location: e.target.value })}
          data-testid={`service-location-${service.id}`}
        />
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Input
          label="Start date"
          type="date"
          value={toDateInput(service.startDate)}
          onChange={(e) => edit({ startDate: e.target.value || null })}
          data-testid={`service-start-${service.id}`}
        />
        <Input
          label="End date"
          type="date"
          value={toDateInput(service.endDate)}
          onChange={(e) => edit({ endDate: e.target.value || null })}
          data-testid={`service-end-${service.id}`}
        />
      </div>
      {/* Services assist is about completeness — no single target field, so the
          suggestion is display-only (Dismiss only, no Accept). */}
      <AssistPopover
        requestFn={assistRequest}
        kinds={['Improve', 'SuggestMeasurement']}
        testIdPrefix={`service-assist-${service.id}`}
      />
      <div className="flex items-center justify-between pt-1">
        <LastEditedStamp
          lastEditedAt={service.lastEditedAt}
          lastEditedByUserId={service.lastEditedByUserId}
          currentUserId={currentUserId}
        />
        <RowDeleteButton
          onDelete={() => void handleDelete()}
          label="Delete service"
          testId={`service-delete-${service.id}`}
        />
      </div>
    </Card>
  );
}
