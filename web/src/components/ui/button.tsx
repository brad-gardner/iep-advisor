import type { ButtonHTMLAttributes } from 'react';

type ButtonVariant = 'primary' | 'secondary' | 'amber' | 'ghost' | 'danger';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
}

const variantStyles: Record<ButtonVariant, string> = {
  primary:
    'bg-brand-teal-500 hover:bg-brand-teal-600 text-white border border-transparent',
  secondary:
    'bg-transparent hover:bg-brand-teal-50 text-brand-teal-500 border-[1.5px] border-brand-teal-300',
  amber:
    'bg-brand-amber-400 hover:bg-brand-amber-500 text-white border border-transparent',
  ghost:
    'bg-transparent hover:bg-brand-slate-100 text-brand-slate-600 border border-transparent',
  danger:
    'bg-transparent hover:bg-red-50 text-brand-red border border-transparent',
};

export function Button({ variant = 'primary', className = '', children, ...props }: ButtonProps) {
  return (
    <button
      className={`inline-flex items-center justify-center px-4 py-2 rounded-button text-[13px] font-medium leading-[1.3] transition-colors focus:outline-none focus:ring-1 focus:ring-brand-teal-400 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed ${variantStyles[variant]} ${className}`}
      {...props}
    >
      {children}
    </button>
  );
}
