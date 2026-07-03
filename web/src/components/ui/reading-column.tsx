import { cn } from "@/lib/cn";

interface ReadingColumnProps {
  children: React.ReactNode;
  className?: string;
  "data-testid"?: string;
}

/**
 * Per-region reading-width cap. The app shell is wide (`max-w-7xl`) so
 * data-dense pages can fill it, but long-form / document / reading content
 * (IEP & ETR viewers, comparisons, PDF text) must stay legible — capped at
 * ~65ch (`max-w-prose`, a `ch`-relative measure, WCAG 1.4.8-friendly) rather
 * than stretching to 80rem. Wrap reading blocks in this instead of hard-coding
 * `max-w-*` per page.
 */
export function ReadingColumn({
  children,
  className,
  "data-testid": testId,
}: ReadingColumnProps) {
  return (
    <div
      data-testid={testId}
      className={cn("mx-auto w-full max-w-prose", className)}
    >
      {children}
    </div>
  );
}
