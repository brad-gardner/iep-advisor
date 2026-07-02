import { cn } from '@/lib/cn';

interface SkeletonProps extends React.ComponentPropsWithoutRef<'div'> {
  /** Render as a circle (e.g. an avatar placeholder). Overrides `rounded`. */
  circle?: boolean;
  /** Apply a rounded corner (default true). Ignored when `circle`. */
  rounded?: boolean;
}

/**
 * Decorative loading placeholder. Size/shape come from `className`
 * (e.g. `h-4 w-40`). Purely visual, so it is `aria-hidden`; announce loading
 * state with a sibling `Spinner`/`role="status"`. The pulse is suppressed under
 * `prefers-reduced-motion`. Forwards `className`/`data-testid` via `...rest`.
 */
export function Skeleton({ circle = false, rounded = true, className = '', ...rest }: SkeletonProps) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        'animate-pulse motion-reduce:animate-none bg-brand-slate-200',
        circle ? 'rounded-full' : rounded ? 'rounded-md' : '',
        className
      )}
      {...rest}
    />
  );
}
