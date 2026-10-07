import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/cn';
import { teamMemberName } from '@/features/educator/components/team/team-eligibility';
import { teamRoleLabel } from '@/features/educator/lib/student-enum-labels';
import type { StudentTeamCache } from '../hooks/use-student-team';

interface TeamMemberSelectProps {
  id?: string;
  team: StudentTeamCache | undefined;
  /** The row's current `_ownerUserId`, or undefined when unassigned. */
  value: number | undefined;
  disabled?: boolean;
  onChange: (userId: number | undefined) => void;
  /** A server save warning for this exact row ("not an active team member…"). */
  warning?: string;
  'data-testid'?: string;
  'aria-label'?: string;
}

const baseSelectClass =
  'w-full px-2 py-1.5 bg-white rounded-input text-brand-slate-800 text-sm border focus:outline-none focus:border-brand-teal-500 focus:ring-[3px] focus:ring-brand-teal-50 transition-colors';

/**
 * Owner picker for a goals/services/accommodations/transition row (plan
 * 2026-10-02-002). Lists the student's ACTIVE team members (name + role) plus
 * "Unassigned"; an amber border and "No owner yet" hint show while unset, echoing
 * the mockup. If the row's current owner is no longer an active team member, an
 * informational (non-selectable) option keeps it visible rather than silently
 * blanking the control — the row stays that way until someone picks a real owner.
 *
 * If the team failed to load, the control disables (reassigning blind risks
 * dropping a still-valid owner) and shows "Owner unavailable" with a retry —
 * never "Former team member", which asserts something a failed fetch never
 * actually checked.
 */
export function TeamMemberSelect({
  id,
  team,
  value,
  disabled,
  onChange,
  warning,
  'data-testid': testId,
  'aria-label': ariaLabel,
}: TeamMemberSelectProps) {
  // `educator` alongside `document-authoring`: `teamRoleLabel` below is
  // backed by that staff-only namespace, and this hook call is what makes a
  // language switch re-render this select once its Spanish loads.
  const { t } = useTranslation(['document-authoring', 'educator']);
  const isError = team?.isError ?? false;
  const members = team?.members ?? [];
  const active = [...members]
    .filter((m) => m.isActive)
    .sort((a, b) => teamMemberName(a).localeCompare(teamMemberName(b)));
  const currentIsActive = value != null && active.some((m) => m.userId === value);
  const unset = value == null;

  return (
    <div>
      <select
        id={id}
        aria-label={ariaLabel}
        value={value != null ? String(value) : ''}
        disabled={disabled || team?.isLoading || isError}
        onChange={(e) => onChange(e.target.value ? Number(e.target.value) : undefined)}
        className={cn(baseSelectClass, unset && !isError ? 'border-brand-amber-400' : 'border-brand-slate-200')}
        data-testid={testId}
      >
        <option value="">{t('teamMemberSelect.unassigned')}</option>
        {isError
          ? value != null && (
              <option value={value} disabled>
                {t('teamMemberSelect.ownerUnavailableOption')}
              </option>
            )
          : value != null &&
            !currentIsActive && (
              <option value={value} disabled>
                {t('teamMemberSelect.formerTeamMemberOption')}
              </option>
            )}
        {!isError &&
          active.map((m) => (
            <option key={m.userId} value={m.userId}>
              {teamMemberName(m)} — {teamRoleLabel(m.teamRole)}
            </option>
          ))}
      </select>
      {isError ? (
        <p className="mt-1 text-xs text-brand-danger-700" role="alert">
          {t('teamMemberSelect.ownerUnavailableHint')}{' '}
          <button type="button" className="underline hover:no-underline" onClick={() => team?.retry?.()}>
            {t('teamMemberSelect.retry')}
          </button>
        </p>
      ) : (
        unset && <p className="mt-1 text-xs text-brand-amber-600">{t('teamMemberSelect.noOwnerYet')}</p>
      )}
      {warning && (
        <p className="mt-1 text-xs text-brand-danger-700" role="alert">
          {warning}
        </p>
      )}
    </div>
  );
}
