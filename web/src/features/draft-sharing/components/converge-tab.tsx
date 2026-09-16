import { Badge } from '@/components/ui/badge';
import type { DocumentInstanceDetailDto, DocumentInstanceStatus } from '@/features/document-authoring/types';
import { ConvergePanel } from './converge-panel';

interface ConvergeTabProps {
  detail: DocumentInstanceDetailDto;
  /** Switch the page back to the Edit tab (used by jump-to-field). */
  onShowEditor?: () => void;
}

const STATUS_VARIANT: Record<DocumentInstanceStatus, 'neutral' | 'warning' | 'success'> = {
  Draft: 'neutral',
  Finalizing: 'warning',
  Finalized: 'success',
};

/** The document page's "Converge" tab (`?tab=converge`): same document
 *  identity heading as the Edit tab, with the converge aggregate below. */
export function ConvergeTab({ detail, onShowEditor }: ConvergeTabProps) {
  return (
    <div className="space-y-6" data-testid="converge-tab">
      <div className="flex items-center gap-3">
        <h1 className="font-serif text-2xl text-brand-slate-800">{detail.documentTypeDisplayName}</h1>
        <Badge variant={STATUS_VARIANT[detail.status]}>{detail.status}</Badge>
      </div>
      <ConvergePanel
        instanceId={detail.id}
        status={detail.status}
        templateVersion={detail.templateVersion}
        onBeforeJump={onShowEditor}
      />
    </div>
  );
}
