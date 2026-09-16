import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "./button";
import { Select } from "./input";

export const PAGE_SIZE_OPTIONS = [25, 50, 100] as const;

interface PaginationProps {
  /** 1-based current page. */
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  /** When provided, a page-size picker is shown. */
  onPageSizeChange?: (pageSize: number) => void;
  /** Accessible name for the `<nav>`, e.g. "Students pagination". */
  label: string;
  "data-testid"?: string;
}

/**
 * Server-paged list footer: "Showing a–b of n", Previous/Next, and an
 * optional page-size picker. A `<nav>` landmark with a live-region summary so
 * AT hears the range update as pages change. Renders nothing when there is
 * nothing to page through and no size picker to show.
 */
export function Pagination({
  page,
  pageSize,
  total,
  onPageChange,
  onPageSizeChange,
  label,
  "data-testid": testId,
}: PaginationProps) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const first = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, total);

  if (total <= pageSize && !onPageSizeChange) return null;

  return (
    <nav
      aria-label={label}
      data-testid={testId}
      className="flex flex-wrap items-center justify-between gap-3 text-sm text-brand-slate-600"
    >
      <p aria-live="polite" data-testid={testId ? `${testId}-summary` : undefined}>
        {total === 0
          ? "No results"
          : `Showing ${first}–${last} of ${total}`}
      </p>

      <div className="flex flex-wrap items-center gap-3">
        {onPageSizeChange && (
          <div className="flex items-center gap-2">
            <Select
              id={testId ? `${testId}-page-size` : "pagination-page-size"}
              label="Rows per page"
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              className="w-auto"
              data-testid={testId ? `${testId}-page-size` : undefined}
            >
              {PAGE_SIZE_OPTIONS.map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </Select>
          </div>
        )}

        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="sm"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
            data-testid={testId ? `${testId}-prev` : undefined}
          >
            <ChevronLeft className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
            Previous
          </Button>
          <span className="px-1 text-xs text-brand-slate-500">
            Page {Math.min(page, pageCount)} of {pageCount}
          </span>
          <Button
            variant="ghost"
            size="sm"
            disabled={page >= pageCount}
            onClick={() => onPageChange(page + 1)}
            data-testid={testId ? `${testId}-next` : undefined}
          >
            Next
            <ChevronRight className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
          </Button>
        </div>
      </div>
    </nav>
  );
}
