import { useEffect, useState } from "react";
import { useParams, useNavigate, Link, Outlet } from "react-router-dom";
import type { ChildProfile, CreateChildProfileRequest } from "@/types/api";
import { getChild, updateChild, deleteChild } from "../api/children-api";
import { ChildForm } from "./child-form";
import { SharedBadge } from "@/features/sharing/components/shared-badge";
import { Button } from "@/components/ui/button";
import { TabsNav, TabLink } from "@/components/ui/tabs";

export function ChildDetailPage() {
  const { childId: childIdParam } = useParams<{ childId: string }>();
  const childId = Number(childIdParam);
  const navigate = useNavigate();
  const [child, setChild] = useState<ChildProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const reloadChild = async () => {
    const response = await getChild(childId);
    if (response.success && response.data) {
      setChild(response.data);
    }
  };

  useEffect(() => {
    async function load() {
      try {
        await reloadChild();
      } catch {
        // handled by interceptor
      } finally {
        setIsLoading(false);
      }
    }
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [childId]);

  const handleUpdate = async (data: CreateChildProfileRequest) => {
    try {
      const response = await updateChild(childId, data);
      if (response.success) {
        const refreshed = await getChild(childId);
        if (refreshed.success && refreshed.data) {
          setChild(refreshed.data);
        }
        setIsEditing(false);
        return { success: true };
      }
      return { success: false, error: response.message || "Update failed" };
    } catch {
      return { success: false, error: "An error occurred" };
    }
  };

  const handleDelete = async () => {
    if (!confirm("Are you sure you want to remove this child profile?")) return;
    setIsDeleting(true);
    try {
      const response = await deleteChild(childId);
      if (response.success) {
        navigate("/children");
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsDeleting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!child) {
    return (
      <div className="text-center py-12">
        <p className="text-brand-slate-400">Child profile not found.</p>
        <Link
          to="/children"
          className="text-brand-teal-500 hover:underline mt-2 inline-block"
        >
          Back to children
        </Link>
      </div>
    );
  }

  if (isEditing) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <h1 className="font-serif">Edit {child.firstName}</h1>
          <Button variant="ghost" onClick={() => setIsEditing(false)}>
            Cancel
          </Button>
        </div>
        <ChildForm
          initialValues={{
            firstName: child.firstName,
            lastName: child.lastName ?? "",
            dateOfBirth: child.dateOfBirth?.split("T")[0] ?? "",
            gradeLevel: child.gradeLevel ?? "",
            disabilityCategory: child.disabilityCategory ?? "",
            schoolDistrict: child.schoolDistrict ?? "",
          }}
          onSubmit={handleUpdate}
          submitLabel="Save Changes"
        />
      </div>
    );
  }

  const isOwner = child.role === "owner";
  const base = `/children/${childId}`;

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div className="flex items-center gap-3">
          <h1 className="font-serif">
            {child.firstName} {child.lastName}
          </h1>
          {!isOwner && <SharedBadge role={child.role} />}
        </div>
        <div className="flex gap-2">
          {isOwner && (
            <Button
              variant="secondary"
              onClick={() => setIsEditing(true)}
              data-testid="child-edit-button"
            >
              Edit
            </Button>
          )}
          {isOwner && (
            <Button
              variant="danger"
              onClick={handleDelete}
              disabled={isDeleting}
              data-testid="child-remove-button"
            >
              {isDeleting ? "Removing..." : "Remove"}
            </Button>
          )}
        </div>
      </div>

      <TabsNav>
        <TabLink to={`${base}/overview`} testId="tab-overview">
          Overview
        </TabLink>
        <TabLink to={`${base}/goals`} testId="tab-goals">
          Goals
        </TabLink>
        <TabLink to={`${base}/ieps`} testId="tab-ieps">
          IEPs
        </TabLink>
        <TabLink to={`${base}/etrs`} testId="tab-etrs">
          ETRs
        </TabLink>
      </TabsNav>

      <Outlet
        context={{ child, childId, reloadChild } satisfies ChildOutletContext}
      />
    </div>
  );
}

export interface ChildOutletContext {
  child: ChildProfile;
  childId: number;
  reloadChild: () => Promise<void>;
}
