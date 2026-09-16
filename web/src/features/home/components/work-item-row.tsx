import { Link } from 'react-router-dom';

interface WorkItemRowProps {
  /** Primary label, e.g. the student's name. */
  title: string;
  /** Secondary line, e.g. document type or meeting time. */
  subtitle?: string;
  /** Trailing slot — a status chip, date, or badge. Never the only cue for
   * status (pair with text/icon in the badge itself). */
  meta?: React.ReactNode;
  /** Drilldown target — every work item links to its student/document/meeting. */
  href: string;
  'data-testid'?: string;
}

/** One row in a home-page work list: a whole-row link to the underlying
 * student/document/meeting, title + subtitle on the left, a status/date slot
 * on the right. */
export function WorkItemRow({ title, subtitle, meta, href, 'data-testid': testId }: WorkItemRowProps) {
  return (
    <li>
      <Link
        to={href}
        data-testid={testId}
        className="-mx-3 flex items-center justify-between gap-3 rounded-card px-3 py-2.5 transition-colors hover:bg-brand-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400"
      >
        <span className="min-w-0">
          <span className="block truncate text-sm font-medium text-brand-slate-800">{title}</span>
          {subtitle && (
            <span className="block truncate text-xs text-brand-slate-500">{subtitle}</span>
          )}
        </span>
        {meta && <span className="flex shrink-0 items-center gap-2 text-xs text-brand-slate-500">{meta}</span>}
      </Link>
    </li>
  );
}
