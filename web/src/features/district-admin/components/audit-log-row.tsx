import { useTranslation } from 'react-i18next';
import { AUDIT_ACTIONS } from '../types';
import type { AuditAction, AuditLogEntry } from '../types';

// A type-guarded membership check (rather than a plain `Set.has`) so the
// `t()` call below gets a narrowed `AuditAction`, not the DTO's loose
// `string` — a template-literal key built from `string` can't be checked
// against the strict per-namespace key union (see `docs/i18n/README.md`'s
// "Typed keys").
function isAuditAction(action: string): action is AuditAction {
  return (AUDIT_ACTIONS as readonly string[]).includes(action);
}

interface AuditLogRowProps {
  entry: AuditLogEntry;
}

// One audit entry: "<actor> <verb> <resource> [with <recipient>] — <timestamp>".
// The display fields already carry server-side fallbacks ("Former staff member",
// "Deleted draft #123"), so they render verbatim.
export function AuditLogRow({ entry }: AuditLogRowProps) {
  const { t } = useTranslation('district-admin');
  // Falls back to a lowercased raw action string so an unrecognized/newer
  // action still renders a sensible row rather than a raw translation key.
  const verb = isAuditAction(entry.action)
    ? t(`auditLog.actionVerbs.${entry.action}`)
    : entry.action.toLowerCase();

  return (
    <div
      data-testid={`audit-row-${entry.id}`}
      className="flex flex-col gap-1 px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
    >
      <p className="text-sm text-brand-slate-700">
        <span className="font-medium text-brand-slate-800">{entry.actorName}</span> {verb}{' '}
        <span className="font-medium text-brand-slate-800">{entry.resourceDisplayName}</span>
        {entry.recipientName && (
          <>
            {' '}
            {t('auditLog.with')}{' '}
            <span className="font-medium text-brand-slate-800">{entry.recipientName}</span>
          </>
        )}
      </p>
      <time
        dateTime={entry.createdAt}
        className="shrink-0 text-xs text-brand-slate-500 sm:text-right"
      >
        {new Date(entry.createdAt).toLocaleString()}
      </time>
    </div>
  );
}
