import { cn } from '@/lib/cn';

type BadgeVariant = 'success' | 'warning' | 'error' | 'info' | 'neutral';

interface BadgeProps extends React.ComponentPropsWithoutRef<'span'> {
  variant?: BadgeVariant;
}

const variantStyles: Record<BadgeVariant, string> = {
  // success keeps the positive teal treatment…
  success: 'bg-brand-teal-50 text-brand-teal-600 border-brand-teal-100',
  warning: 'bg-brand-amber-50 text-brand-amber-500 border-brand-amber-100',
  // …error now rides the dedicated danger scale (was raw red-*).
  error: 'bg-brand-danger-50 text-brand-danger-700 border-brand-danger-200',
  // …and info moves to a stronger neutral slate so it reads distinctly from
  // both success (teal) and neutral (the lighter slate-50/600).
  info: 'bg-brand-slate-100 text-brand-slate-700 border-brand-slate-200',
  neutral: 'bg-brand-slate-50 text-brand-slate-600 border-brand-slate-200',
};

export function Badge({ variant = 'neutral', children, className = '', ...rest }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center px-2 py-0.5 rounded-badge text-xs font-medium border',
        variantStyles[variant],
        className
      )}
      {...rest}
    >
      {children}
    </span>
  );
}
