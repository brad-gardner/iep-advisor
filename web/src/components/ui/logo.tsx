import { CheckCircle } from 'lucide-react';

interface LogoProps {
  variant?: 'light' | 'dark';
  size?: 'sm' | 'md' | 'lg';
  showTagline?: boolean;
}

const sizes = {
  sm: { icon: 24, text: 'text-lg', tagline: 'text-[8px]' },
  md: { icon: 32, text: 'text-2xl', tagline: 'text-[10px]' },
  lg: { icon: 40, text: 'text-3xl', tagline: 'text-[11px]' },
};

export function Logo({ variant = 'light', size = 'md', showTagline = true }: LogoProps) {
  const s = sizes[size];
  const textColor = variant === 'dark' ? 'text-white' : 'text-brand-slate-800';

  return (
    <div className="flex items-center gap-2.5">
      <div className="bg-brand-teal-500 rounded-full p-1.5 flex items-center justify-center">
        <CheckCircle className="text-white" size={s.icon * 0.6} strokeWidth={2.5} />
      </div>
      <div>
        <div className={`${s.text} font-serif leading-none`}>
          <span className={textColor}>IEP </span>
          <span className="text-brand-teal-500 font-semibold">Advisor</span>
        </div>
        {showTagline && (
          <p className={`${s.tagline} font-semibold uppercase tracking-[0.12em] ${variant === 'dark' ? 'text-brand-slate-400' : 'text-brand-slate-400'} mt-0.5`}>
            Navigate with confidence
          </p>
        )}
      </div>
    </div>
  );
}
