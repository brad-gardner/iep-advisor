import { Badge } from '@/components/ui/badge';
import { signatureStatusLabel } from '../lib/signature-status-label';
import type { SignatureStatus } from '../types';

const VARIANT: Record<SignatureStatus, 'warning' | 'info' | 'success'> = {
  Unsigned: 'warning',
  PartiallySigned: 'info',
  Signed: 'success',
};

interface SignatureStatusBadgeProps {
  status: SignatureStatus;
  'data-testid'?: string;
}

/** Print/sign status chip for a finalized version (plan 7, decision 4). */
export function SignatureStatusBadge({ status, 'data-testid': testId }: SignatureStatusBadgeProps) {
  return (
    <Badge variant={VARIANT[status]} data-testid={testId}>
      {signatureStatusLabel(status)}
    </Badge>
  );
}
