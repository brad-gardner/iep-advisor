import { type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';

interface EmptyStateProps extends Omit<React.ComponentPropsWithoutRef<'div'>, 'title'> {
  /** A lucide icon component (e.g. `Users`). Rendered muted. */
  icon?: LucideIcon;
  title: string;
  description?: string;
  /** Slot for a primary action (e.g. a `Button` or link). */
  action?: React.ReactNode;
}

/**
 * Guidance-oriented empty state: a muted icon, a serif title, an optional
 * explanation, and an optional action slot — never a blank region. Forwards
 * `className`/`data-testid` via `...rest`.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className = '',
  ...rest
}: EmptyStateProps) {
  return (
    <div
      className={cn('flex flex-col items-center text-center px-6 py-12', className)}
      {...rest}
    >
      {Icon && (
        <div className="mb-4 text-brand-slate-300">
          <Icon className="w-8 h-8" strokeWidth={1.5} aria-hidden="true" />
        </div>
      )}
      <h3 className="font-serif text-lg text-brand-slate-800">{title}</h3>
      {description && (
        <p className="mt-1 max-w-sm text-sm text-brand-slate-500">{description}</p>
      )}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
