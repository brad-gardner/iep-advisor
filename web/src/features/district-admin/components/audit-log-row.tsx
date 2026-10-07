import { Trans, useTranslation } from 'react-i18next';
import { getActiveLanguage } from '@/lib/i18n/format';
import { AUDIT_ACTIONS } from '../types';
import type { AuditAction, AuditLogEntry } from '../types';

// A type-guarded membership check (rather than a plain `Set.has`) so the
// `Trans` key below gets a narrowed `AuditAction`, not the DTO's loose
// `string` — a template-literal key built from `string` can't be checked
// against the strict per-namespace key union (see `docs/i18n/README.md`'s
// "Typed keys").
function isAuditAction(action: string): action is AuditAction {
  return (AUDIT_ACTIONS as readonly string[]).includes(action);
}

interface AuditLogRowProps {
  entry: AuditLogEntry;
}

// One audit entry: "<actor> <action sentence, incl. resource> [with <recipient>]" plus a timestamp.
// The display fields already carry server-side fallbacks ("Former staff member",
// "Deleted draft #123"), so they render verbatim.
export function AuditLogRow({ entry }: AuditLogRowProps) {
  const { t } = useTranslation('district-admin');
  const action = isAuditAction(entry.action) ? entry.action : null;

  return (
    <div
      data-testid={`audit-row-${entry.id}`}
      className="flex flex-col gap-1 px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
    >
      <p className="text-sm text-brand-slate-700">
        {action ? (
          // One interpolated sentence per action (a `withRecipient` context
          // variant covers the optional recipient clause) rather than
          // building the sentence from concatenated `t()` fragments and raw
          // JSX around a value — see `docs/i18n/README.md`'s note on mixed-
          // language sentences. `actorName`/`resourceName`/`recipientName`
          // are user/resource-provided text, so they're escaped during
          // interpolation and unescaped only for display (same pattern as
          // `studentAcceptInvite.invitedAsSentence`), keeping a literal "<"
          // in one of them from ever being parsed as one of the tags below.
          <Trans
            t={t}
            i18nKey={`auditLog.actionSentences.${action}`}
            context={entry.recipientName ? 'withRecipient' : undefined}
            values={{
              actorName: entry.actorName,
              resourceName: entry.resourceDisplayName,
              recipientName: entry.recipientName ?? undefined,
            }}
            tOptions={{ interpolation: { escapeValue: true } }}
            shouldUnescape
            components={{
              actor: <span className="font-medium text-brand-slate-800" />,
              resource: <span className="font-medium text-brand-slate-800" />,
              recipient: <span className="font-medium text-brand-slate-800" />,
            }}
          />
        ) : (
          // An unrecognized/newer action has no translation key — render a
          // sensible row anyway rather than a raw translation key.
          <>
            <span className="font-medium text-brand-slate-800">{entry.actorName}</span>{' '}
            {entry.action.toLowerCase()}{' '}
            <span className="font-medium text-brand-slate-800">{entry.resourceDisplayName}</span>
            {entry.recipientName && (
              <>
                {' '}
                {t('auditLog.with')}{' '}
                <span className="font-medium text-brand-slate-800">{entry.recipientName}</span>
              </>
            )}
          </>
        )}
      </p>
      <time
        dateTime={entry.createdAt}
        className="shrink-0 text-xs text-brand-slate-500 sm:text-right"
      >
        {new Date(entry.createdAt).toLocaleString(getActiveLanguage())}
      </time>
    </div>
  );
}
