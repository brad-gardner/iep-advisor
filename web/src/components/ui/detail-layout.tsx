import { cn } from "@/lib/cn";

interface DetailLayoutProps {
  /** Primary content (drafts, tabs, document body). Comes first in the DOM. */
  main: React.ReactNode;
  /** Right rail: status, metadata, quick actions. */
  sidebar: React.ReactNode;
  className?: string;
  "data-testid"?: string;
}

/**
 * Two-column detail scaffold: a wide main column and a right sidebar for
 * status/metadata/quick-actions. **Main comes first in source order** so
 * screen-reader and keyboard users reach the primary content before the rail;
 * the sidebar is visually placed right via grid, not DOM order. Collapses to a
 * single stacked column (main first) below `md`.
 */
export function DetailLayout({
  main,
  sidebar,
  className,
  "data-testid": testId,
}: DetailLayoutProps) {
  return (
    <div
      data-testid={testId}
      className={cn(
        "grid grid-cols-1 gap-6 md:grid-cols-[minmax(0,1fr)_18rem] lg:gap-8",
        className,
      )}
    >
      <div className="min-w-0">{main}</div>
      <aside className="space-y-4 md:sticky md:top-8 md:self-start">
        {sidebar}
      </aside>
    </div>
  );
}
