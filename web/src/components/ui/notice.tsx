import { Info, AlertTriangle, XCircle, CheckCircle } from 'lucide-react';
import { cn } from '@/lib/cn';

type NoticeVariant = 'info' | 'warning' | 'error' | 'success';

interface NoticeProps extends React.ComponentPropsWithoutRef<'div'> {
  variant?: NoticeVariant;
  title: string;
  children?: React.ReactNode;
}

const config: Record<NoticeVariant, { bg: string; text: string; border: string; Icon: typeof Info }> = {
  // info reads as a calm, neutral slate — distinct from the positive teal
  // success treatment below (previously the two were identical).
  info: {
    bg: 'bg-brand-slate-50',
    text: 'text-brand-slate-700',
    border: 'border-brand-slate-200',
    Icon: Info,
  },
  success: {
    bg: 'bg-brand-teal-50',
    text: 'text-brand-teal-600',
    border: 'border-brand-teal-100',
    Icon: CheckCircle,
  },
  warning: {
    bg: 'bg-brand-amber-50',
    text: 'text-brand-amber-500',
    border: 'border-brand-amber-100',
    Icon: AlertTriangle,
  },
  // error moves off raw red-* onto the dedicated danger scale.
  error: {
    bg: 'bg-brand-danger-50',
    text: 'text-brand-danger-700',
    border: 'border-brand-danger-200',
    Icon: XCircle,
  },
};

export function Notice({ variant = 'info', title, children, className = '', ...rest }: NoticeProps) {
  const { bg, text, border, Icon } = config[variant];

  return (
    <div className={cn(bg, border, 'border rounded-card p-4 flex gap-3', className)} {...rest}>
      <Icon className={`w-5 h-5 ${text} shrink-0 mt-0.5`} strokeWidth={1.8} aria-hidden="true" />
      <div>
        <p className={`text-sm font-medium ${text}`}>{title}</p>
        {children && <div className="text-sm text-brand-slate-600 mt-1">{children}</div>}
      </div>
    </div>
  );
}
