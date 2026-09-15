import { UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface RosterBulkBarProps {
  selectedCount: number;
  onAssignCaseManager: () => void;
  onClear: () => void;
}

// Appears above the roster once rows are selected (admins only). The count is
// announced by the page's persistent live region (this bar mounts with its
// text, which AT would not read).
export function RosterBulkBar({
  selectedCount,
  onAssignCaseManager,
  onClear,
}: RosterBulkBarProps) {
  if (selectedCount === 0) return null;
  return (
    <div
      role="region"
      aria-label="Bulk actions"
      className="flex flex-wrap items-center justify-between gap-3 rounded-card border border-brand-teal-100 bg-brand-teal-50 px-4 py-2 text-sm"
      data-testid="roster-bulk-bar"
    >
      <span className="text-brand-teal-600">
        {selectedCount} selected
      </span>
      <div className="flex items-center gap-2">
        <Button
          size="sm"
          onClick={onAssignCaseManager}
          data-testid="roster-bulk-assign-case-manager"
        >
          <UserCheck className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
          Assign case manager
        </Button>
        <Button variant="ghost" size="sm" onClick={onClear} data-testid="roster-bulk-clear">
          Clear
        </Button>
      </div>
    </div>
  );
}
