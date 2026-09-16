import { Card } from '@/components/ui/card';

interface HomeSectionProps {
  title: string;
  /** Date range or scope hint under the title, e.g. "Sep 15 – Sep 21". */
  subtitle?: string;
  /** Top-right slot, e.g. a "View all" link. */
  action?: React.ReactNode;
  'data-testid'?: string;
  children: React.ReactNode;
}

/** The standard card shell for a home-page section: a serif `<h2>` (+ optional
 * subtitle/action) over its content. Every staff/parent/student home section
 * composes inside this so loading, empty, and populated states line up. */
export function HomeSection({
  title,
  subtitle,
  action,
  'data-testid': testId,
  children,
}: HomeSectionProps) {
  return (
    <Card data-testid={testId}>
      <div className="mb-4 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-serif text-lg text-brand-slate-800">{title}</h2>
          {subtitle && <p className="text-sm text-brand-slate-500">{subtitle}</p>}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </div>
      {children}
    </Card>
  );
}
