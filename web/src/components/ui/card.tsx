interface CardProps extends React.ComponentPropsWithoutRef<'div'> {
  children: React.ReactNode;
  className?: string;
  accent?: boolean;
}

export function Card({ children, className = '', accent, ...rest }: CardProps) {
  return (
    <div
      className={`bg-white rounded-card border-[0.5px] border-brand-slate-200 p-6 ${accent ? 'border-l-2 border-l-brand-teal-500' : ''} ${className}`}
      {...rest}
    >
      {children}
    </div>
  );
}
