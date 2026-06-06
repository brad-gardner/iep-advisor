interface EmptyHintProps {
  children: React.ReactNode;
}

export function EmptyHint({ children }: EmptyHintProps) {
  return (
    <p className="text-sm text-brand-slate-400 italic py-2" data-testid="empty-hint">
      {children}
    </p>
  );
}
