import { useEffect, useState } from "react";
import { useParams, useNavigate, Link, Outlet } from "react-router-dom";
import { UserX } from "lucide-react";
import type { ChildProfile, CreateChildProfileRequest } from "@/types/api";
import { getChild, updateChild, deleteChild } from "../api/children-api";
import { ChildForm } from "./child-form";
import { SharedBadge } from "@/features/sharing/components/shared-badge";
import { SchoolLinkBadge } from "@/features/child-links/components/school-link-badge";
import { getChildSchoolLinks } from "@/features/child-links/api/child-links-api";
import type { ChildSchoolLink } from "@/features/child-links/types";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { PageLayout } from "@/components/ui/page-layout";
import { useToast } from "@/components/ui/toast";
import { TabsNav, TabLink } from "@/components/ui/tabs";

export function ChildDetailPage() {
  const { childId: childIdParam } = useParams<{ childId: string }>();
  const childId = Number(childIdParam);
  const navigate = useNavigate();
  const { show: showToast } = useToast();
  const [child, setChild] = useState<ChildProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [schoolLinks, setSchoolLinks] = useState<ChildSchoolLink[]>([]);

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

  useEffect(() => {
    if (!childId) return;
    let active = true;
    getChildSchoolLinks(childId)
      .then((res) => {
        if (active && res.success && res.data) {
          setSchoolLinks(res.data);
        }
      })
      .catch(() => {
        // Non-critical: the badge simply won't render.
      });
    return () => {
      active = false;
    };
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
        showToast({ message: "Changes saved", variant: "success" });
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
        showToast({ message: "Child profile removed", variant: "success" });
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
        <Spinner label="Loading child…" />
      </div>
    );
  }

  if (!child) {
    return (
      <EmptyState
        icon={UserX}
        title="Child profile not found."
        action={
          <Link to="/children">
            <Button variant="secondary">Back to children</Button>
          </Link>
        }
      />
    );
  }

  if (isEditing) {
    return (
      <PageLayout
        title={`Edit ${child.firstName}`}
        actions={
          <Button variant="ghost" onClick={() => setIsEditing(false)}>
            Cancel
          </Button>
        }
      >
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
      </PageLayout>
    );
  }

  const isOwner = child.role === "owner";
  const base = `/children/${childId}`;

  return (
    <PageLayout
      title={`${child.firstName} ${child.lastName ?? ""}`.trim()}
      breadcrumb={[
        { label: "Children", to: "/children" },
        { label: `${child.firstName} ${child.lastName ?? ""}`.trim() },
      ]}
      actions={
        isOwner ? (
          <>
            <Button
              variant="secondary"
              onClick={() => setIsEditing(true)}
              data-testid="child-edit-button"
            >
              Edit
            </Button>
            <Button
              variant="danger"
              onClick={handleDelete}
              loading={isDeleting}
              data-testid="child-remove-button"
            >
              Remove
            </Button>
          </>
        ) : undefined
      }
    >
      {(child.role !== "owner" || schoolLinks.length > 0) && (
        <div className="flex flex-wrap items-center gap-2">
          {!isOwner && <SharedBadge role={child.role} />}
          <SchoolLinkBadge links={schoolLinks} />
        </div>
      )}

      <TabsNav>
        <TabLink to={`${base}/overview`} testId="tab-overview">
          Overview
        </TabLink>
        <TabLink to={`${base}/goals`} testId="tab-goals">
          Goals
        </TabLink>
        <TabLink to={`${base}/analysis`} testId="tab-analysis">
          Analysis
        </TabLink>
        <TabLink to={`${base}/meeting-prep`} testId="tab-meeting-prep">
          Meeting Prep
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
    </PageLayout>
  );
}

export interface ChildOutletContext {
  child: ChildProfile;
  childId: number;
  reloadChild: () => Promise<void>;
}
