import type { ButtonHTMLAttributes } from 'react';
import { cn } from '@/lib/cn';
import { Spinner } from './spinner';

type ButtonVariant = 'primary' | 'secondary' | 'amber' | 'ghost' | 'danger';
type ButtonSize = 'sm' | 'md' | 'lg';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  /** Visual size. `md` (default) is pixel-identical to the historical button. */
  size?: ButtonSize;
  /**
   * When true the button is disabled (guards against double-submit), exposes
   * `aria-busy`, and overlays an inline spinner while reserving its resting
   * width (the label stays in the DOM, just visually hidden) so nothing shifts.
   */
  loading?: boolean;
}

const variantStyles: Record<ButtonVariant, string> = {
  // AA fix (2026-09-20): white-on-teal-500 measured 3.78:1 (fails the 4.5:1
  // text floor since every size renders normal-size text). Moved one step
  // down the existing ramp to teal-600 (6.90:1); hover darkens further to
  // teal-700 so the resting/hover relationship is preserved.
  primary:
    'bg-brand-teal-600 hover:bg-brand-teal-700 text-white border border-transparent',
  // AA fix (2026-09-20): teal-500 text on white measured 3.78:1. Moved to
  // teal-600 (6.90:1) to match the primary fill above.
  secondary:
    'bg-transparent hover:bg-brand-teal-50 text-brand-teal-600 border-[1.5px] border-brand-teal-300',
  // AA fix (2026-09-20): white-on-amber-400 measured 3.00:1. Moved to
  // amber-500 (4.75:1); hover darkens further to amber-600.
  amber:
    'bg-brand-amber-500 hover:bg-brand-amber-600 text-white border border-transparent',
  ghost:
    'bg-transparent hover:bg-brand-slate-100 text-brand-slate-600 border border-transparent',
  danger:
    'bg-transparent hover:bg-brand-danger-50 text-brand-danger-700 border border-transparent',
};

// `md` reproduces the original padding/size exactly so every existing call site
// renders unchanged; `sm`/`lg` are the opt-in additions.
const sizeStyles: Record<ButtonSize, string> = {
  sm: 'px-3 py-1.5 text-xs',
  md: 'px-4 py-2 text-[13px]',
  lg: 'px-5 py-2.5 text-sm',
};

const baseStyles =
  'inline-flex items-center justify-center rounded-button font-medium leading-[1.3] transition-colors focus:outline-none focus:ring-1 focus:ring-brand-teal-400 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed';

export function Button({
  variant = 'primary',
  size = 'md',
  loading = false,
  className = '',
  children,
  disabled,
  ...props
}: ButtonProps) {
  // Loading is a distinct DOM shape (overlaid spinner + width-reserving label).
  // The resting path renders `children` directly so call sites that rely on the
  // button's own flex layout (icon `gap`/margins) are byte-for-byte unchanged.
  if (loading) {
    return (
      <button
        className={cn(baseStyles, sizeStyles[size], variantStyles[variant], 'relative', className)}
        disabled
        aria-busy="true"
        {...props}
      >
        <span className="absolute inset-0 flex items-center justify-center" aria-hidden="true">
          {/* `tone="current"` tracks the button's text colour so the ring stays
              visible on filled variants (white on teal/amber) and on-brand on
              the light ones. */}
          <Spinner size="sm" tone="current" />
        </span>
        {/* Kept in the accessibility tree (opacity, not `hidden`) so the button
            retains its accessible name and reserves its resting width. */}
        <span className="inline-flex items-center opacity-0">{children}</span>
      </button>
    );
  }

  return (
    <button
      className={cn(baseStyles, sizeStyles[size], variantStyles[variant], className)}
      disabled={disabled}
      {...props}
    >
      {children}
    </button>
  );
}
