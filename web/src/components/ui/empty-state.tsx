import { isValidElement } from 'react';
import { type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';

interface EmptyStateProps extends Omit<React.ComponentPropsWithoutRef<'div'>, 'title'> {
  /** A lucide icon component (e.g. `Users`) or an already-rendered element. Rendered muted. */
  icon?: LucideIcon | React.ReactElement;
  title: string;
  description?: string;
  /** Slot for a primary action (e.g. a `Button` or link). */
  action?: React.ReactNode;
}

function renderIcon(icon: EmptyStateProps['icon']) {
  if (!icon) return null;
  // An already-rendered node (e.g. `<Users />`) passes straight through.
  if (isValidElement(icon)) return icon;
  // Otherwise treat it as an icon component type (lucide icons are forwardRef
  // exotics, so `typeof` is unreliable — anything non-element is a component).
  const Icon = icon as LucideIcon;
  return <Icon className="w-8 h-8" strokeWidth={1.5} aria-hidden="true" />;
}

/**
 * Guidance-oriented empty state: a muted icon, a serif title, an optional
 * explanation, and an optional action slot — never a blank region. Forwards
 * `className`/`data-testid` via `...rest`.
 */
export function EmptyState({
  icon,
  title,
  description,
  action,
  className = '',
  ...rest
}: EmptyStateProps) {
  const iconNode = renderIcon(icon);
  return (
    <div
      className={cn('flex flex-col items-center text-center px-6 py-12', className)}
      {...rest}
    >
      {iconNode && <div className="mb-4 text-brand-slate-300">{iconNode}</div>}
      <h3 className="font-serif text-lg text-brand-slate-800">{title}</h3>
      {description && (
        <p className="mt-1 max-w-sm text-sm text-brand-slate-500">{description}</p>
      )}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
