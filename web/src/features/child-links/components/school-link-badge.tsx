import { Badge } from '@/components/ui/badge';
import type { ChildSchoolLink } from '../types';

interface SchoolLinkBadgeProps {
  links: ChildSchoolLink[];
}

// Renders a small "Linked to {school}" indicator when a child has any active
// school link. Renders nothing when there are none, so it never disrupts layout.
export function SchoolLinkBadge({ links }: SchoolLinkBadgeProps) {
  if (links.length === 0) return null;

  const schoolName = links[0].schoolName;
  const label = schoolName ? `Linked to ${schoolName}` : 'Linked to school';

  return (
    <Badge variant="info" data-testid="school-link-badge">
      {label}
    </Badge>
  );
}
