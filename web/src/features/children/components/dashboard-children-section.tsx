import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Users, Plus } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/notice";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { SharedBadge } from "@/features/sharing/components/shared-badge";
import { useChildren } from "@/features/children/hooks/use-children";
import { gradeLevelLabel } from "@/lib/grade-level-label";
import type { ChildProfile } from "@/types/api";

const MAX_DISPLAY = 4;

function sortChildren(children: ChildProfile[]): ChildProfile[] {
  return [...children].sort((a, b) => {
    if (a.role === "owner" && b.role !== "owner") return -1;
    if (a.role !== "owner" && b.role === "owner") return 1;
    return a.firstName.localeCompare(b.firstName);
  });
}

export function DashboardChildrenSection() {
  const { t } = useTranslation("children");
  const { children, isLoading, error, reload } = useChildren();

  if (isLoading) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">
          {t("dashboard.heading")}
        </h2>
        <div className="flex justify-center py-12">
          <Spinner label={t("dashboard.loadingChildren")} />
        </div>
      </section>
    );
  }

  if (error) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">
          {t("dashboard.heading")}
        </h2>
        <Notice variant="error" title={t("dashboard.loadErrorTitle")}>
          <Button variant="ghost" size="sm" onClick={reload} className="mt-1">
            {t("common:ui.tryAgain")}
          </Button>
        </Notice>
      </section>
    );
  }

  if (!children.length) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">
          {t("dashboard.heading")}
        </h2>
        <Card>
          <EmptyState
            icon={Users}
            title={t("dashboard.emptyTitle")}
            action={
              <Link to="/children">
                <Button>{t("dashboard.addFirstChild")}</Button>
              </Link>
            }
          />
        </Card>
      </section>
    );
  }

  const sorted = sortChildren(children);
  const displayed = sorted.slice(0, MAX_DISPLAY);
  const hasMore = children.length > MAX_DISPLAY;

  return (
    <section data-testid="dashboard-children-section">
      <div className="flex items-center justify-between mb-4">
        <h2 className="font-serif text-lg text-brand-slate-800">{t("dashboard.heading")}</h2>
        <Link
          to="/children"
          className="flex items-center gap-1 text-sm text-brand-teal-500 hover:text-brand-teal-400"
        >
          <Plus className="h-4 w-4" strokeWidth={1.8} />
          {t("dashboard.add")}
        </Link>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {displayed.map((child) => (
          <Link key={child.id} to={`/children/${child.id}`} className="block">
            <Card
              className="hover:border-brand-teal-200 transition-colors"
              data-testid="dashboard-child-card"
            >
              <div className="flex items-center gap-2">
                <h3 className="font-serif text-brand-slate-800 truncate">
                  {child.firstName} {child.lastName}
                </h3>
                {child.role !== "owner" && <SharedBadge role={child.role} />}
              </div>
              {(child.gradeLevel || child.schoolDistrict) && (
                <div className="mt-2 flex flex-wrap gap-3 text-xs text-brand-slate-500">
                  {child.gradeLevel && (
                    <span>{t("dashboard.gradePrefix", { grade: gradeLevelLabel(child.gradeLevel) })}</span>
                  )}
                  {child.schoolDistrict && <span>{child.schoolDistrict}</span>}
                </div>
              )}
            </Card>
          </Link>
        ))}
      </div>
      {hasMore && (
        <Link
          to="/children"
          className="mt-3 block text-sm text-brand-teal-500 hover:text-brand-teal-400"
        >
          {t("dashboard.viewAll", { count: children.length })}
        </Link>
      )}
    </section>
  );
}
