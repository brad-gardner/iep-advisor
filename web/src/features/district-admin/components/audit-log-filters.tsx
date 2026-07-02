import { useEffect, useState } from 'react';
import { Input, Select } from '@/components/ui/input';
import { getStaffList } from '@/features/staff-invites/api/staff-invites-api';
import type { StaffMember } from '@/features/staff-invites/types';
import { AUDIT_ACTIONS } from '../types';
import type { AuditAction, AuditLogFilters as AuditLogFiltersValue } from '../types';

// Past-tense labels matching how the audit rows read ("Jane viewed …"), so the
// filter's voice is consistent with the results it produces.
const ACTION_LABELS: Record<AuditAction, string> = {
  View: 'Viewed',
  Edit: 'Edited',
  Share: 'Shared',
  Export: 'Exported',
  Finalize: 'Finalized',
};

interface AuditLogFiltersProps {
  onChange: (filters: AuditLogFiltersValue) => void;
}

// The raw form state — plain strings straight from the inputs. Assembled into a
// typed filter object (dates converted to UTC instants) before emitting.
interface FilterFields {
  staffUserId: string;
  action: string;
  from: string;
  to: string;
}

const EMPTY_FIELDS: FilterFields = { staffUserId: '', action: '', from: '', to: '' };

// A native date input yields a local calendar day ("YYYY-MM-DD"). Convert the
// lower bound to the start of that local day and the upper bound to the end
// (23:59:59.999 local), then to a UTC instant, so the backend's inclusive upper
// bound covers the whole selected day regardless of the viewer's timezone.
function toStartOfDayUtc(dateStr: string): string | undefined {
  if (!dateStr) return undefined;
  const [year, month, day] = dateStr.split('-').map(Number);
  return new Date(year, month - 1, day, 0, 0, 0, 0).toISOString();
}

function toEndOfDayUtc(dateStr: string): string | undefined {
  if (!dateStr) return undefined;
  const [year, month, day] = dateStr.split('-').map(Number);
  return new Date(year, month - 1, day, 23, 59, 59, 999).toISOString();
}

function assemble(fields: FilterFields): AuditLogFiltersValue {
  const filters: AuditLogFiltersValue = {};
  if (fields.staffUserId) filters.staffUserId = Number(fields.staffUserId);
  if (fields.action) filters.action = fields.action as AuditAction;
  const fromUtc = toStartOfDayUtc(fields.from);
  const toUtc = toEndOfDayUtc(fields.to);
  if (fromUtc) filters.fromUtc = fromUtc;
  if (toUtc) filters.toUtc = toUtc;
  return filters;
}

// Filters for the district audit-log viewer: a staff-member dropdown (the roster
// includes deactivated staff, labeled so a FERPA reviewer can still find their
// history), an action dropdown, and a local-day date range. Emits an assembled,
// UTC-normalized filter object on every change.
export function AuditLogFilters({ onChange }: AuditLogFiltersProps) {
  const [staff, setStaff] = useState<StaffMember[]>([]);
  const [fields, setFields] = useState<FilterFields>(EMPTY_FIELDS);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getStaffList();
        if (active && response.success && response.data) {
          setStaff(response.data.members);
        }
      } catch {
        if (active) setStaff([]);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  // Update one field, then emit the newly-assembled filter object from the same
  // event handler (never an effect) so the parent can reset + refetch page one.
  const update = (patch: Partial<FilterFields>) => {
    const next = { ...fields, ...patch };
    setFields(next);
    onChange(assemble(next));
  };

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Select
        id="audit-log-staff-filter"
        label="Staff member"
        value={fields.staffUserId}
        onChange={(e) => update({ staffUserId: e.target.value })}
        data-testid="audit-log-staff-filter"
      >
        <option value="">All staff</option>
        {staff.map((member) => (
          <option key={member.staffProfileId} value={member.userId}>
            {member.firstName} {member.lastName}
            {member.isActive ? '' : ' (deactivated)'}
          </option>
        ))}
      </Select>

      <Select
        id="audit-log-action-filter"
        label="Action"
        value={fields.action}
        onChange={(e) => update({ action: e.target.value })}
        data-testid="audit-log-action-filter"
      >
        <option value="">All actions</option>
        {AUDIT_ACTIONS.map((action) => (
          <option key={action} value={action}>
            {ACTION_LABELS[action]}
          </option>
        ))}
      </Select>

      <Input
        id="audit-log-from-filter"
        label="From"
        type="date"
        value={fields.from}
        onChange={(e) => update({ from: e.target.value })}
        data-testid="audit-log-from-filter"
      />

      <Input
        id="audit-log-to-filter"
        label="To"
        type="date"
        value={fields.to}
        onChange={(e) => update({ to: e.target.value })}
        data-testid="audit-log-to-filter"
      />
    </div>
  );
}
