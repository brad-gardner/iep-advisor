import { useState } from 'react';
import { Share2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import type { DocumentInstanceStatus } from '@/features/document-authoring/types';
import { useRecipientPreview } from '../hooks/use-recipient-preview';
import { ShareWithFamilyModal } from './share-with-family-modal';
import type { SharedDraftRevisionDto } from '../types';

interface ShareWithFamilyButtonProps {
  instanceId: number;
  status: DocumentInstanceStatus;
  label?: string;
  variant?: 'primary' | 'secondary';
  onShared?: (revision: SharedDraftRevisionDto) => void;
}

const SHAREABLE_STATUSES: DocumentInstanceStatus[] = ['Draft', 'Finalizing'];

/**
 * Gated entry point for sharing: hidden entirely (not just disabled) while the
 * district's policy is off, the instance isn't Draft/Finalizing, or the
 * recipient preview hasn't loaded yet — never a flash of a doomed-to-403
 * button. Reused for the editor header's "Share with family" and the
 * Converge tab's "Share again".
 */
export function ShareWithFamilyButton({ instanceId, status, label = 'Share with family', variant = 'secondary', onShared }: ShareWithFamilyButtonProps) {
  const { preview, isLoading } = useRecipientPreview(instanceId);
  const { show: showToast } = useToast();
  const [open, setOpen] = useState(false);

  const eligible = SHAREABLE_STATUSES.includes(status);
  if (isLoading || !preview || !preview.policyEnabled || !eligible) return null;

  return (
    <>
      <Button variant={variant} size="sm" onClick={() => setOpen(true)} data-testid="share-with-family-open">
        <Share2 className="mr-1 h-4 w-4" aria-hidden="true" />
        {label}
      </Button>
      <ShareWithFamilyModal
        open={open}
        onClose={() => setOpen(false)}
        instanceId={instanceId}
        onShared={(revision) => {
          setOpen(false);
          showToast({ message: `Shared as revision ${revision.revisionNumber}`, variant: 'success' });
          onShared?.(revision);
        }}
      />
    </>
  );
}
