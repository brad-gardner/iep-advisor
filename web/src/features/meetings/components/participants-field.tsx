import { useState } from 'react';
import { UserPlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { TEAM_ROLE_LABELS } from '@/features/educator/types';
import type { ParticipantRow } from '../hooks/use-meeting-participant-pool';

const KIND_LABEL: Record<ParticipantRow['kind'], string> = {
  team: 'IEP team',
  eligible: 'Other staff',
  family: 'Family',
  student: 'Student',
  external: 'External guest',
};

interface ParticipantsFieldProps {
  rows: ParticipantRow[];
  onToggle: (key: string) => void;
  onRequiredChange: (key: string, isRequired: boolean) => void;
  onAddExternal: (row: { name: string; email: string }) => void;
  onRemoveExternal: (key: string) => void;
}

/** One participant checkbox row: toggle, name/role, and a "required" toggle. */
function ParticipantRowItem({
  row,
  onToggle,
  onRequiredChange,
  onRemove,
}: {
  row: ParticipantRow;
  onToggle: () => void;
  onRequiredChange: (isRequired: boolean) => void;
  onRemove?: () => void;
}) {
  const checkboxId = `participant-${row.key}`;
  return (
    <div className="flex items-center justify-between gap-3 py-1.5" data-testid={`participant-row-${row.key}`}>
      <div className="flex min-w-0 items-center gap-2">
        <input
          id={checkboxId}
          type="checkbox"
          checked={row.checked}
          onChange={onToggle}
          className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"
          data-testid={`participant-checkbox-${row.key}`}
        />
        <label htmlFor={checkboxId} className="min-w-0 truncate text-sm text-brand-slate-800">
          {row.displayName}
          <span className="ml-1.5 text-xs text-brand-slate-500">
            {row.kind === 'team' ? TEAM_ROLE_LABELS[row.teamRole] : KIND_LABEL[row.kind]}
          </span>
        </label>
      </div>
      <div className="flex shrink-0 items-center gap-3">
        <label className="flex items-center gap-1.5 text-xs text-brand-slate-500">
          <input
            type="checkbox"
            checked={row.isRequired}
            disabled={!row.checked}
            onChange={(e) => onRequiredChange(e.target.checked)}
            className="h-3.5 w-3.5 rounded border-brand-slate-300 text-brand-teal-500 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"
            data-testid={`participant-required-${row.key}`}
          />
          Required
        </label>
        {onRemove && (
          <Button variant="ghost" size="sm" onClick={onRemove} data-testid={`participant-remove-${row.key}`}>
            Remove
          </Button>
        )}
      </div>
    </div>
  );
}

/**
 * Participant checklist for the schedule form: the candidate pool (team,
 * other eligible staff, family) plus a small inline form for adding an
 * external guest by name/email. The parent owns the row state so it can be
 * merged with an existing meeting's participants on reschedule.
 */
// Deliberately permissive (shape only, not full RFC 5322) — good enough to
// catch a plain typo before it silently becomes an invite nobody receives;
// the server still validates for real (plan4-fix-contract.md item 3).
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function ParticipantsField({
  rows,
  onToggle,
  onRequiredChange,
  onAddExternal,
  onRemoveExternal,
}: ParticipantsFieldProps) {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const trimmedEmail = email.trim();
  // An external guest can only ever be reached (and invited) by email, so —
  // unlike the pool's real-user rows — a valid email is required, not just
  // "some text in one of the two fields."
  const emailInvalid = trimmedEmail !== '' && !EMAIL_PATTERN.test(trimmedEmail);
  const canAdd = trimmedEmail !== '' && !emailInvalid;
  const emailErrorId = 'external-participant-email-error';

  const handleAdd = () => {
    if (!canAdd) return;
    onAddExternal({ name: name.trim(), email: trimmedEmail });
    setName('');
    setEmail('');
  };

  const pool = rows.filter((r) => r.kind !== 'external');
  const external = rows.filter((r) => r.kind === 'external');

  return (
    <div className="space-y-3">
      <span className="block text-[13px] font-medium text-brand-slate-600">Participants</span>
      <div className="rounded-input border border-brand-slate-200 divide-y divide-brand-slate-100 px-3">
        {pool.map((row) => (
          <ParticipantRowItem
            key={row.key}
            row={row}
            onToggle={() => onToggle(row.key)}
            onRequiredChange={(isRequired) => onRequiredChange(row.key, isRequired)}
          />
        ))}
        {external.map((row) => (
          <ParticipantRowItem
            key={row.key}
            row={row}
            onToggle={() => onToggle(row.key)}
            onRequiredChange={(isRequired) => onRequiredChange(row.key, isRequired)}
            onRemove={() => onRemoveExternal(row.key)}
          />
        ))}
        {pool.length === 0 && external.length === 0 && (
          <p className="py-3 text-sm text-brand-slate-500">No participants yet.</p>
        )}
      </div>

      <div className="flex flex-col gap-2 sm:flex-row sm:items-start">
        <Input
          id="external-participant-name"
          label="External participant name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Optional"
        />
        <div className="flex-1">
          <Input
            id="external-participant-email"
            label="Email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="name@example.com"
            aria-invalid={emailInvalid || undefined}
            aria-describedby={emailInvalid ? emailErrorId : undefined}
          />
          {emailInvalid && (
            <p id={emailErrorId} className="mt-1 text-xs text-brand-danger-600">
              Enter a valid email address
            </p>
          )}
        </div>
        <Button
          type="button"
          variant="secondary"
          size="sm"
          onClick={handleAdd}
          disabled={!canAdd}
          data-testid="add-external-participant"
        >
          <UserPlus className="mr-1.5 h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
          Add
        </Button>
      </div>
    </div>
  );
}
