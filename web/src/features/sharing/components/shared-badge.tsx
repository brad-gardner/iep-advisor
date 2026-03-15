import { Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface SharedBadgeProps {
  role: string;
}

export function SharedBadge({ role }: SharedBadgeProps) {
  const label = role.charAt(0).toUpperCase() + role.slice(1);

  return (
    <Badge variant="info">
      <Users className="w-3 h-3 mr-1" strokeWidth={1.8} aria-hidden="true" />
      Shared &middot; {label}
    </Badge>
  );
}
