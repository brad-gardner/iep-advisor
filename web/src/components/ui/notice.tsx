import { Info, AlertTriangle, XCircle, CheckCircle } from 'lucide-react';

type NoticeVariant = 'info' | 'warning' | 'error' | 'success';

interface NoticeProps {
  variant?: NoticeVariant;
  title: string;
  children?: React.ReactNode;
}

const config: Record<NoticeVariant, { bg: string; text: string; border: string; Icon: typeof Info }> = {
  info: {
    bg: 'bg-brand-teal-50',
    text: 'text-brand-teal-600',
    border: 'border-brand-teal-100',
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
  error: {
    bg: 'bg-red-50',
    text: 'text-brand-red',
    border: 'border-red-200',
    Icon: XCircle,
  },
};

export function Notice({ variant = 'info', title, children }: NoticeProps) {
  const { bg, text, border, Icon } = config[variant];

  return (
    <div className={`${bg} ${border} border rounded-card p-4 flex gap-3`}>
      <Icon className={`w-5 h-5 ${text} shrink-0 mt-0.5`} strokeWidth={1.8} aria-hidden="true" />
      <div>
        <p className={`text-sm font-medium ${text}`}>{title}</p>
        {children && <div className="text-sm text-brand-slate-600 mt-1">{children}</div>}
      </div>
    </div>
  );
}
