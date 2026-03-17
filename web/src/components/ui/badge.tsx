type BadgeVariant = 'success' | 'warning' | 'error' | 'info' | 'neutral';

interface BadgeProps extends React.ComponentPropsWithoutRef<'span'> {
  variant?: BadgeVariant;
  children: React.ReactNode;
  className?: string;
}

const variantStyles: Record<BadgeVariant, string> = {
  success: 'bg-brand-teal-50 text-brand-teal-600 border-brand-teal-100',
  warning: 'bg-brand-amber-50 text-brand-amber-500 border-brand-amber-100',
  error: 'bg-red-50 text-brand-red border-red-200',
  info: 'bg-brand-teal-50 text-brand-teal-600 border-brand-teal-100',
  neutral: 'bg-brand-slate-50 text-brand-slate-600 border-brand-slate-200',
};

export function Badge({ variant = 'neutral', children, className = '', ...rest }: BadgeProps) {
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-badge text-xs font-medium border ${variantStyles[variant]} ${className}`}
      {...rest}
    >
      {children}
    </span>
  );
}
