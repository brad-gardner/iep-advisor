import { useTranslation } from 'react-i18next';
import { Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { sharingRoleLabel } from '../lib/role-label';

interface SharedBadgeProps {
  role: string;
}

export function SharedBadge({ role }: SharedBadgeProps) {
  const { t } = useTranslation('sharing');

  return (
    <Badge variant="info">
      <Users className="w-3 h-3 mr-1" strokeWidth={1.8} aria-hidden="true" />
      {t('sharedBadge.prefix', { role: sharingRoleLabel(role) })}
    </Badge>
  );
}
