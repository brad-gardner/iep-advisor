import { cn } from '@/lib/cn';
import { PageHeader, type Breadcrumb } from './page-header';

interface PageLayoutProps {
  /** Rendered as the single page `<h1>` via `PageHeader`. */
  title: string;
  subtitle?: string;
  breadcrumb?: Breadcrumb[];
  /** Top-right action slot (see `PageHeader`). */
  actions?: React.ReactNode;
  children: React.ReactNode;
  /** Extra classes on the outer stack (rarely needed). */
  className?: string;
  /** Forwarded to the outer element for e2e/test hooks. */
  'data-testid'?: string;
}

/**
 * The standard page shell: a `PageHeader` (title/subtitle/breadcrumb/actions)
 * over a stacked content region. It composes *inside* `MainLayout` — it does
 * not replace it, so `MainLayout` still owns the `max-w-5xl` container. The
 * header and each child are direct siblings of a single `space-y-6` stack, so
 * the spacing matches the hand-rolled `space-y-6 + <h1>` pattern it replaces.
 */
export function PageLayout({
  title,
  subtitle,
  breadcrumb,
  actions,
  children,
  className,
  'data-testid': testId,
}: PageLayoutProps) {
  return (
    <div className={cn('space-y-6', className)} data-testid={testId}>
      <PageHeader title={title} subtitle={subtitle} breadcrumb={breadcrumb} actions={actions} />
      {children}
    </div>
  );
}
