import type { AuditLogEntry } from '../types';

// Human-readable past-tense verb per action. Falls back to a lowercased raw
// action string so an unrecognized/newer action still renders a sensible row.
const ACTION_VERBS: Record<string, string> = {
  View: 'viewed',
  Edit: 'edited',
  Share: 'shared',
  Export: 'exported',
  Finalize: 'finalized',
};

interface AuditLogRowProps {
  entry: AuditLogEntry;
}

// One audit entry: "<actor> <verb> <resource> [with <recipient>] — <timestamp>".
// The display fields already carry server-side fallbacks ("Former staff member",
// "Deleted draft #123"), so they render verbatim.
export function AuditLogRow({ entry }: AuditLogRowProps) {
  const verb = ACTION_VERBS[entry.action] ?? entry.action.toLowerCase();

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
            with{' '}
            <span className="font-medium text-brand-slate-800">{entry.recipientName}</span>
          </>
        )}
      </p>
      <time
        dateTime={entry.createdAt}
        className="shrink-0 text-xs text-brand-slate-400 sm:text-right"
      >
        {new Date(entry.createdAt).toLocaleString()}
      </time>
    </div>
  );
}
