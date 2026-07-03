import { Fragment } from 'react';
import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { cn } from '@/lib/cn';

export interface Breadcrumb {
  label: string;
  /** When present the crumb is a link; the final crumb is usually left plain. */
  to?: string;
}

interface PageHeaderProps {
  /** Rendered as the single page `<h1>`. */
  title: string;
  subtitle?: string;
  /** Optional breadcrumb chain. */
  breadcrumb?: Breadcrumb[];
  /**
   * Optional top-right action slot. Compose the page's buttons here — place the
   * dominant (primary) action last so it reads as the emphasised control.
   */
  actions?: React.ReactNode;
}

function Crumb({ crumb, isLast }: { crumb: Breadcrumb; isLast: boolean }) {
  const className = 'max-w-[12rem] truncate';
  if (isLast || !crumb.to) {
    return (
      <span
        className={cn(className, isLast ? 'text-brand-slate-600 font-medium' : 'text-brand-slate-400')}
        aria-current={isLast ? 'page' : undefined}
      >
        {crumb.label}
      </span>
    );
  }
  return (
    <Link to={crumb.to} className={cn(className, 'text-brand-slate-400 hover:text-brand-slate-600 transition-colors')}>
      {crumb.label}
    </Link>
  );
}

/**
 * Owns the single page `<h1>` plus optional subtitle, breadcrumb, and a
 * top-right action slot. Rendered inside `PageLayout` (which lives inside
 * `MainLayout`); it does not provide the page container itself.
 */
export function PageHeader({ title, subtitle, breadcrumb, actions }: PageHeaderProps) {
  const crumbs = breadcrumb && breadcrumb.length > 0 ? breadcrumb : null;

  return (
    <header className="space-y-2">
      {crumbs && (
        <nav aria-label="Breadcrumb">
          <ol className="flex items-center gap-1.5 text-xs">
            {crumbs.map((crumb, i) => (
              <Fragment key={i}>
                {i > 0 && (
                  <ChevronRight
                    className="w-3.5 h-3.5 text-brand-slate-300 shrink-0"
                    strokeWidth={2}
                    aria-hidden="true"
                  />
                )}
                <li className="flex min-w-0 items-center">
                  <Crumb crumb={crumb} isLast={i === crumbs.length - 1} />
                </li>
              </Fragment>
            ))}
          </ol>
        </nav>
      )}

      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="font-serif">{title}</h1>
          {subtitle && <p className="mt-1 text-sm text-brand-slate-500">{subtitle}</p>}
        </div>
        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
    </header>
  );
}
