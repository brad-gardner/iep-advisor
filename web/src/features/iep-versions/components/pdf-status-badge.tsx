import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';

interface PdfStatusBadgeProps {
  // Accepts the summary's nullable string or the typed status from the detail.
  status: string | null | undefined;
}

// Small read-only badge mapping a PDF render status to a colored label.
export function PdfStatusBadge({ status }: PdfStatusBadgeProps) {
  const { t } = useTranslation('iep-versions');
  if (status === 'Rendered') {
    return (
      <Badge variant="success" data-testid="pdf-status-badge">
        {t('statusBadge.ready')}
      </Badge>
    );
  }
  if (status === 'Error') {
    return (
      <Badge variant="error" data-testid="pdf-status-badge">
        {t('statusBadge.failed')}
      </Badge>
    );
  }
  // null / Pending / unknown → still generating
  return (
    <Badge variant="warning" data-testid="pdf-status-badge">
      {t('statusBadge.generating')}
    </Badge>
  );
}
