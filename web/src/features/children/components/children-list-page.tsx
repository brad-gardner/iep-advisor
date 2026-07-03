import { useState } from "react";
import { Link } from "react-router-dom";
import { Users, Plus } from "lucide-react";
import { useChildren } from "../hooks/use-children";
import { createChild } from "../api/children-api";
import { ChildForm } from "./child-form";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Drawer } from "@/components/ui/drawer";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { PageLayout } from "@/components/ui/page-layout";
import { useToast } from "@/components/ui/toast";
import { SharedBadge } from "@/features/sharing/components/shared-badge";
import type { CreateChildProfileRequest } from "@/types/api";

export function ChildrenListPage() {
  const { children, isLoading, reload } = useChildren();
  const { show: showToast } = useToast();
  const [isAddOpen, setIsAddOpen] = useState(false);

  const handleCreate = async (data: CreateChildProfileRequest) => {
    try {
      const response = await createChild(data);
      if (response.success) {
        await reload();
        setIsAddOpen(false);
        showToast({ message: "Child profile added", variant: "success" });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || "Failed to create child profile",
      };
    } catch {
      return { success: false, error: "An error occurred" };
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading children…" />
      </div>
    );
  }

  return (
    <PageLayout
      title="Your Children"
      actions={
        <Button
          onClick={() => setIsAddOpen(true)}
          data-testid="add-child-button"
        >
          <Plus className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
          Add Child
        </Button>
      }
    >
      {children.length === 0 ? (
        <EmptyState
          icon={Users}
          title="No child profiles yet."
          action={
            <Button
              onClick={() => setIsAddOpen(true)}
              data-testid="add-first-child-button"
            >
              Add Your First Child
            </Button>
          }
          data-testid="children-empty-state"
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {children.map((child) => (
            <Link key={child.id} to={`/children/${child.id}`} className="block">
              <Card
                className="hover:border-brand-teal-200 transition-colors"
                data-testid="child-card"
              >
                <h3 className="font-serif text-brand-slate-800">
                  {child.firstName} {child.lastName}
                </h3>
                {child.role !== "owner" && (
                  <div className="mt-1">
                    <SharedBadge role={child.role} />
                  </div>
                )}
                <div className="mt-2 flex flex-wrap gap-3 text-xs text-brand-slate-400">
                  {child.gradeLevel && <span>Grade: {child.gradeLevel}</span>}
                  {child.disabilityCategory && (
                    <span>{child.disabilityCategory}</span>
                  )}
                  {child.schoolDistrict && <span>{child.schoolDistrict}</span>}
                </div>
              </Card>
            </Link>
          ))}
        </div>
      )}

      <Drawer
        open={isAddOpen}
        onClose={() => setIsAddOpen(false)}
        title="Add child"
        size="lg"
        data-testid="add-child-drawer"
      >
        <ChildForm
          onSubmit={handleCreate}
          submitLabel="Create Profile"
          embedded
        />
      </Drawer>
    </PageLayout>
  );
}
