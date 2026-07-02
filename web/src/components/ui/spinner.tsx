import { cn } from '@/lib/cn';

type SpinnerSize = 'sm' | 'md' | 'lg';
type SpinnerTone = 'brand' | 'current';

interface SpinnerProps extends React.ComponentPropsWithoutRef<'div'> {
  size?: SpinnerSize;
  /**
   * Ring colour. `brand` (default) is the teal ring; `current` inherits the
   * parent's text colour (`currentColor`) so the spinner stays visible on a
   * filled surface — e.g. a white ring inside a teal/amber Button.
   */
  tone?: SpinnerTone;
  /** Accessible loading text, exposed to screen readers via the status role. */
  label?: string;
}

const sizeStyles: Record<SpinnerSize, string> = {
  sm: 'h-4 w-4',
  md: 'h-8 w-8',
  lg: 'h-10 w-10',
};

const toneStyles: Record<SpinnerTone, string> = {
  brand: 'border-brand-teal-500',
  current: 'border-current',
};

/**
 * The one spinner. `role="status"` + an sr-only label announce the loading
 * state; the spin is suppressed under `prefers-reduced-motion`. Forwards
 * `className` and `data-testid` (and any other div props) via `...rest`.
 */
export function Spinner({
  size = 'md',
  tone = 'brand',
  label = 'Loading…',
  className = '',
  ...rest
}: SpinnerProps) {
  return (
    <div
      role="status"
      className={cn(
        'inline-block animate-spin motion-reduce:animate-none rounded-full border-b-2',
        toneStyles[tone],
        sizeStyles[size],
        className
      )}
      {...rest}
    >
      <span className="sr-only">{label}</span>
    </div>
  );
}
