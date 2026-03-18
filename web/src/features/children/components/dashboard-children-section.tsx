import { Link } from "react-router-dom";
import { Users, Plus } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/notice";
import { SharedBadge } from "@/features/sharing/components/shared-badge";
import { useChildren } from "@/features/children/hooks/use-children";
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
  const { children, isLoading, error, reload } = useChildren();

  if (isLoading) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">
          My Children
        </h2>
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      </section>
    );
  }

  if (error) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">
          My Children
        </h2>
        <Notice variant="error" title="Couldn't load children">
          <button onClick={reload} className="text-sm underline">
            Try again
          </button>
        </Notice>
      </section>
    );
  }

  if (!children.length) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">
          My Children
        </h2>
        <Card className="text-center py-12">
          <Users
            className="mx-auto h-12 w-12 text-brand-slate-300"
            strokeWidth={1.8}
            aria-hidden="true"
          />
          <p className="mt-3 text-sm text-brand-slate-400">
            No child profiles yet
          </p>
          <Link to="/children/new" className="mt-4 inline-block">
            <Button>Add your first child profile</Button>
          </Link>
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
        <h2 className="font-serif text-lg text-brand-slate-800">My Children</h2>
        <Link
          to="/children/new"
          className="flex items-center gap-1 text-sm text-brand-teal-500 hover:text-brand-teal-400"
        >
          <Plus className="h-4 w-4" strokeWidth={1.8} />
          Add
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
                <div className="mt-2 flex flex-wrap gap-3 text-xs text-brand-slate-400">
                  {child.gradeLevel && <span>Grade: {child.gradeLevel}</span>}
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
          View all ({children.length})
        </Link>
      )}
    </section>
  );
}
