import { useMemo, useState } from "react";
import { ChevronUp, ChevronDown, ChevronsUpDown } from "lucide-react";
import { cn } from "@/lib/cn";
import { Menu, type MenuItem } from "./menu";
import { Skeleton } from "./skeleton";

export type SortDirection = "asc" | "desc";

export interface TableColumn<T> {
  key: string;
  header: string;
  /** Cell content for a row. */
  cell: (row: T) => React.ReactNode;
  /**
   * Comparable value for client-side sorting. Providing it makes the column
   * sortable (a `<button>`-in-`<th>` header with `aria-sort`).
   */
  sortValue?: (row: T) => string | number;
  align?: "left" | "right";
  /** Hide the column below the given breakpoint (responsive de-densify). */
  hideBelow?: "md" | "lg";
  /** Extra classes on the cell/header. */
  className?: string;
}

interface TableProps<T> {
  /** Accessible name for both the scroll region and the `<table>`. */
  label: string;
  columns: TableColumn<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  /** Initial sort; also the fallback the header toggle returns through. */
  defaultSort?: { key: string; direction: SortDirection };
  /** Whole-row navigation. The actions kebab cell stops propagation. */
  onRowClick?: (row: T) => void;
  /** Per-row actions rendered as a trailing kebab `Menu`. */
  rowActions?: (row: T) => MenuItem[];
  /** Accessible label for a row's kebab, e.g. `(row) => row.name`. */
  rowActionLabel?: (row: T) => string;
  loading?: boolean;
  loadingRows?: number;
  /** Rendered (spanning all columns) when there are no rows and not loading. */
  empty?: React.ReactNode;
  "data-testid"?: string;
}

/**
 * Above this row count, client-side sort should give way to server-side paged
 * sort. Exported so callers can guard against overfeeding the table.
 */
export const CLIENT_SORT_ROW_CEILING = 500;

function compare(a: string | number, b: string | number): number {
  if (typeof a === "number" && typeof b === "number") return a - b;
  return String(a).localeCompare(String(b));
}

/**
 * Data table on a native `<table>` (not an ARIA grid). Column headers with a
 * `sortValue` become keyboard-operable sort buttons carrying `aria-sort` — only
 * the single active header gets the attribute, and the icon encodes direction
 * by shape (not color). Rows support whole-row click navigation alongside a
 * trailing kebab actions cell that stops propagation, so the two never fight.
 * Loading renders skeleton rows; empty renders a full-width `empty` slot.
 *
 * Sort is client-side — fine at pilot volumes. Above ~{@link
 * CLIENT_SORT_ROW_CEILING} rows this should move server-side (paged sort);
 * TODO when a surface approaches that.
 */
export function Table<T>({
  label,
  columns,
  rows,
  rowKey,
  defaultSort,
  onRowClick,
  rowActions,
  rowActionLabel,
  loading = false,
  loadingRows = 5,
  empty,
  "data-testid": testId,
}: TableProps<T>) {
  const [sort, setSort] = useState<
    { key: string; direction: SortDirection } | undefined
  >(defaultSort);

  const hasActions = Boolean(rowActions);
  const totalCols = columns.length + (hasActions ? 1 : 0);

  const sortedRows = useMemo(() => {
    if (!sort) return rows;
    const col = columns.find((c) => c.key === sort.key);
    if (!col?.sortValue) return rows;
    const factor = sort.direction === "asc" ? 1 : -1;
    // Decorate-sort-undecorate keeps ties stable regardless of engine.
    return rows
      .map((row, index) => ({ row, index }))
      .sort((a, b) => {
        const result = compare(col.sortValue!(a.row), col.sortValue!(b.row));
        return result !== 0 ? result * factor : a.index - b.index;
      })
      .map((entry) => entry.row);
  }, [rows, sort, columns]);

  const toggleSort = (key: string) => {
    setSort((prev) => {
      if (prev?.key === key) {
        return { key, direction: prev.direction === "asc" ? "desc" : "asc" };
      }
      return { key, direction: "asc" };
    });
  };

  const alignClass = (align?: "left" | "right") =>
    align === "right" ? "text-right" : "text-left";
  const hideClass = (hideBelow?: "md" | "lg") =>
    hideBelow === "md"
      ? "hidden md:table-cell"
      : hideBelow === "lg"
        ? "hidden lg:table-cell"
        : "";

  return (
    <div
      role="region"
      aria-label={label}
      tabIndex={0}
      data-testid={testId}
      className="overflow-x-auto rounded-card border border-brand-slate-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400"
    >
      <table className="w-full border-collapse text-sm" aria-label={label}>
        <thead>
          <tr className="border-b border-brand-slate-200 bg-brand-slate-50">
            {columns.map((col, colIndex) => {
              const isActive = sort?.key === col.key;
              const sortable = Boolean(col.sortValue);
              return (
                <th
                  key={col.key}
                  scope="col"
                  aria-sort={
                    isActive
                      ? sort!.direction === "asc"
                        ? "ascending"
                        : "descending"
                      : undefined
                  }
                  className={cn(
                    "whitespace-nowrap px-4 py-2.5 text-xs font-medium text-brand-slate-500",
                    alignClass(col.align),
                    hideClass(col.hideBelow),
                    // Freeze the first column when the region scrolls horizontally.
                    colIndex === 0 && "sticky left-0 z-10 bg-brand-slate-50",
                    col.className,
                  )}
                >
                  {sortable ? (
                    <button
                      type="button"
                      onClick={() => toggleSort(col.key)}
                      data-testid={
                        testId ? `${testId}-sort-${col.key}` : undefined
                      }
                      className={cn(
                        "inline-flex items-center gap-1 rounded-button font-medium transition-colors hover:text-brand-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400",
                        col.align === "right" && "flex-row-reverse",
                      )}
                    >
                      {col.header}
                      {isActive ? (
                        sort!.direction === "asc" ? (
                          <ChevronUp
                            className="h-3.5 w-3.5"
                            strokeWidth={2}
                            aria-hidden="true"
                          />
                        ) : (
                          <ChevronDown
                            className="h-3.5 w-3.5"
                            strokeWidth={2}
                            aria-hidden="true"
                          />
                        )
                      ) : (
                        <ChevronsUpDown
                          className="h-3.5 w-3.5 text-brand-slate-300"
                          strokeWidth={2}
                          aria-hidden="true"
                        />
                      )}
                    </button>
                  ) : (
                    col.header
                  )}
                </th>
              );
            })}
            {hasActions && (
              <th scope="col" className="w-12 px-2 py-2.5">
                <span className="sr-only">Actions</span>
              </th>
            )}
          </tr>
        </thead>

        <tbody className="divide-y divide-brand-slate-100">
          {loading ? (
            Array.from({ length: loadingRows }).map((_, rowIndex) => (
              <tr key={`skeleton-${rowIndex}`}>
                {columns.map((col, colIndex) => (
                  <td
                    key={col.key}
                    className={cn(
                      "px-4 py-3",
                      hideClass(col.hideBelow),
                      colIndex === 0 && "sticky left-0 bg-white",
                    )}
                  >
                    <Skeleton className="h-4 w-24" />
                  </td>
                ))}
                {hasActions && <td className="px-2 py-3" />}
              </tr>
            ))
          ) : sortedRows.length === 0 ? (
            <tr>
              <td colSpan={totalCols} className="px-4 py-10">
                {empty}
              </td>
            </tr>
          ) : (
            sortedRows.map((row) => {
              const actions = rowActions?.(row);
              return (
                <tr
                  key={rowKey(row)}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  className={cn(
                    "bg-white transition-colors",
                    onRowClick && "cursor-pointer hover:bg-brand-slate-50",
                  )}
                >
                  {columns.map((col, colIndex) => (
                    <td
                      key={col.key}
                      className={cn(
                        "px-4 py-3 text-brand-slate-700",
                        alignClass(col.align),
                        hideClass(col.hideBelow),
                        colIndex === 0 && "sticky left-0 bg-white",
                        col.className,
                      )}
                    >
                      {col.cell(row)}
                    </td>
                  ))}
                  {hasActions && (
                    <td className="px-2 py-3 text-right">
                      {actions && actions.length > 0 && (
                        // Keep the kebab out of the row's click target so
                        // opening the menu never triggers row navigation.
                        <div onClick={(e) => e.stopPropagation()}>
                          <Menu
                            label={
                              rowActionLabel
                                ? `Actions for ${rowActionLabel(row)}`
                                : "Row actions"
                            }
                            items={actions}
                          />
                        </div>
                      )}
                    </td>
                  )}
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
}
