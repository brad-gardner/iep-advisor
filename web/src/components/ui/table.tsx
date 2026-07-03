import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
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
  /** Initial sort. */
  defaultSort?: { key: string; direction: SortDirection };
  /**
   * Row navigation target. Renders the **first column** as a real `<Link>`
   * (keyboard- and screen-reader-navigable) whose hit area is stretched over
   * the whole row, so a mouse click anywhere navigates while the kebab and
   * other interactive cells (which sit above it) keep working.
   */
  rowHref?: (row: T) => string;
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
  rowHref,
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
            {columns.map((col) => {
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
                {columns.map((col) => (
                  <td
                    key={col.key}
                    className={cn("px-4 py-3", hideClass(col.hideBelow))}
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
                {empty ?? (
                  <p className="text-center text-sm text-brand-slate-400">
                    Nothing to show yet.
                  </p>
                )}
              </td>
            </tr>
          ) : (
            sortedRows.map((row) => {
              const actions = rowActions?.(row);
              const href = rowHref?.(row);
              return (
                <tr
                  key={rowKey(row)}
                  className={cn(
                    "relative bg-white transition-colors",
                    href && "hover:bg-brand-slate-50",
                  )}
                >
                  {columns.map((col, colIndex) => (
                    <td
                      key={col.key}
                      className={cn(
                        "px-4 py-3 text-brand-slate-700",
                        alignClass(col.align),
                        hideClass(col.hideBelow),
                        col.className,
                      )}
                    >
                      {href && colIndex === 0 ? (
                        // Real link = keyboard/SR-navigable; the stretched
                        // `before` overlay makes the whole row mouse-clickable.
                        <Link
                          to={href}
                          className="font-medium text-brand-slate-800 before:absolute before:inset-0 hover:underline focus:outline-none focus-visible:underline"
                        >
                          {col.cell(row)}
                        </Link>
                      ) : (
                        col.cell(row)
                      )}
                    </td>
                  ))}
                  {hasActions && (
                    // `relative z-10` lifts the kebab above the row's stretched
                    // link overlay so it stays clickable and never navigates.
                    <td className="relative z-10 px-2 py-3 text-right">
                      {actions && actions.length > 0 && (
                        <Menu
                          label={
                            rowActionLabel
                              ? `Actions for ${rowActionLabel(row)}`
                              : "Row actions"
                          }
                          items={actions}
                        />
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
