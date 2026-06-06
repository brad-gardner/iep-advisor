import { useEffect, useState } from "react";
import { useParams, useNavigate, Link, Outlet } from "react-router-dom";
import type { ChildProfile, CreateChildProfileRequest } from "@/types/api";
import { getChild, updateChild, deleteChild } from "../api/children-api";
import { ChildForm } from "./child-form";
import { SharedBadge } from "@/features/sharing/components/shared-badge";
import { SchoolLinkBadge } from "@/features/child-links/components/school-link-badge";
import { getChildSchoolLinks } from "@/features/child-links/api/child-links-api";
import type { ChildSchoolLink } from "@/features/child-links/types";
import { Button } from "@/components/ui/button";
import { TabsNav, TabLink } from "@/components/ui/tabs";
import { useFeatureFlag } from "@/hooks/use-feature-flags";

export function ChildDetailPage() {
  const { childId: childIdParam } = useParams<{ childId: string }>();
  const childId = Number(childIdParam);
  const navigate = useNavigate();
  const [child, setChild] = useState<ChildProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const analysisEnabled = useFeatureFlag("AnalysisRun");
  const meetingPrepStandalone = useFeatureFlag("MeetingPrepStandalone");
  const schoolSideEnabled = useFeatureFlag("SchoolSide");
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
    if (!schoolSideEnabled || !childId) return;
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
  }, [childId, schoolSideEnabled]);

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
          {schoolSideEnabled && <SchoolLinkBadge links={schoolLinks} />}
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
        {analysisEnabled && (
          <TabLink to={`${base}/analysis`} testId="tab-analysis">
            Analysis
          </TabLink>
        )}
        {meetingPrepStandalone && (
          <TabLink to={`${base}/meeting-prep`} testId="tab-meeting-prep">
            Meeting Prep
          </TabLink>
        )}
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
