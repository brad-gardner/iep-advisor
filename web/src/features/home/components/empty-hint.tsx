interface EmptyHintProps {
  'data-testid'?: string;
  children: React.ReactNode;
}

/**
 * A muted inline note for a section with nothing to show — never an error, and
 * never a blank region. Distinct from `EmptyState`: this is for a small slot
 * inside an already-titled `HomeSection`, so it skips the icon/heading.
 */
export function EmptyHint({ 'data-testid': testId, children }: EmptyHintProps) {
  return (
    <p className="text-sm text-brand-slate-500" data-testid={testId}>
      {children}
    </p>
  );
}
