import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/notice";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { PageLayout } from "@/components/ui/page-layout";
import { DetailLayout } from "@/components/ui/detail-layout";
import { useToast } from "@/components/ui/toast";
import {
  getStudent,
  getStudentLinks,
  inviteParent,
  revokeStudentLink,
} from "../api/educator-api";
import type { ChildLink, SchoolStudent } from "../types";
import { ORG_ROLE } from "../types";
import { useEducatorProfile } from "../hooks/use-educator-profile";
import { InviteParentForm } from "../components/invite-parent-form";
import { StudentLinksList } from "../components/student-links-list";
import { StudentStaffAccessPanel } from "../components/staff-access/student-staff-access-panel";
import { VersionHistoryList } from "@/features/iep-versions/components/version-history-list";
import { useStudentVersions } from "@/features/iep-versions/hooks/use-version-list";
import { InviteStudentForm } from "@/features/student/components/invite-student-form";
import { inviteStudentFromEducator } from "@/features/student/api/student-invite-api";

export function EducatorStudentDetailPage() {
  const { show: showToast } = useToast();
  const { studentId: studentIdParam } = useParams<{ studentId: string }>();
  const studentId = Number(studentIdParam);

  const [student, setStudent] = useState<SchoolStudent | null>(null);
  const [links, setLinks] = useState<ChildLink[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [revokingId, setRevokingId] = useState<number | null>(null);
  const [revokeNote, setRevokeNote] = useState<string | null>(null);
  const { versions, isLoading: versionsLoading } =
    useStudentVersions(studentId);
  const { profile } = useEducatorProfile();
  const canManageStaffAccess =
    profile?.orgRoleId === ORG_ROLE.DistrictAdmin ||
    profile?.orgRoleId === ORG_ROLE.SchoolAdmin;

  const handleInviteStudent = async (email: string) => {
    try {
      const response = await inviteStudentFromEducator(studentId, email);
      return { success: response.success, message: response.message };
    } catch {
      return {
        success: false,
        message: "An error occurred sending the invitation",
      };
    }
  };

  const reloadLinks = useCallback(async () => {
    try {
      const response = await getStudentLinks(studentId);
      if (response.success && response.data) {
        setLinks(response.data);
      }
    } catch {
      // Refetch failure keeps the existing links rather than crashing the page.
    }
  }, [studentId]);

  useEffect(() => {
    async function load() {
      try {
        const studentRes = await getStudent(studentId);
        if (studentRes.success && studentRes.data) {
          setStudent(studentRes.data);
        }
        await reloadLinks();
      } catch {
        // A server/network error leaves `student` null → the "not found" state
        // renders, rather than surfacing an unhandled rejection.
      } finally {
        setIsLoading(false);
      }
    }
    load();
  }, [studentId, reloadLinks]);

  const handleInvite = async (email: string) => {
    try {
      const response = await inviteParent(studentId, { parentEmail: email });
      if (response.success) {
        await reloadLinks();
        showToast({ message: "Parent invited", variant: "success" });
        return { success: true, message: response.message };
      }
      return { success: false, message: response.message };
    } catch {
      return {
        success: false,
        message: "An error occurred sending the invitation",
      };
    }
  };

  const handleRevoke = async (link: ChildLink) => {
    if (
      !confirm(
        "Revoke this parent link? This cannot be undone, and the parent keeps any data already shared.",
      )
    ) {
      return;
    }
    setRevokingId(link.id);
    setRevokeNote(null);
    try {
      const response = await revokeStudentLink(studentId, link.id);
      if (response.success) {
        // Surface the forward-only note from the server (revoke is not retroactive).
        setRevokeNote(
          response.message ||
            "Link revoked. This does not remove access already granted.",
        );
        await reloadLinks();
      }
    } finally {
      setRevokingId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading student…" />
      </div>
    );
  }

  if (!student) {
    return (
      <EmptyState
        title="Student not found"
        description="This student may have been removed, or you may not have access to their record."
        action={
          <Link to="/educator/students">
            <Button variant="secondary">Back to students</Button>
          </Link>
        }
      />
    );
  }

  const studentName =
    `${student.firstName} ${student.lastName ?? ""}`.trim() || "Student";

  return (
    <PageLayout
      title={studentName}
      breadcrumb={[
        { label: "Students", to: "/educator/students" },
        { label: studentName },
      ]}
    >
      <DetailLayout
        main={
          <div className="space-y-6">
            <section className="space-y-3">
              <h2 className="font-serif text-lg">IEP versions</h2>
              <Card data-testid="iep-versions-section">
                <VersionHistoryList
                  versions={versions}
                  isLoading={versionsLoading}
                  linkBase={`/educator/students/${studentId}/iep-versions`}
                />
              </Card>
            </section>

            <section className="space-y-3">
              <h2 className="font-serif text-lg">Assigned staff</h2>
              <StudentStaffAccessPanel
                studentId={studentId}
                studentSchoolId={student.schoolId}
                canManage={canManageStaffAccess}
              />
            </section>

            <InviteParentForm onInvite={handleInvite} />

            <InviteStudentForm
              onInvite={handleInviteStudent}
              description={`Invite ${student.firstName} to activate their own account and take part in their IEP process.`}
            />

            <section className="space-y-3">
              <h2 className="font-serif text-lg">Parent links</h2>
              {revokeNote && (
                <Notice variant="info" title="Link revoked">
                  {revokeNote}
                </Notice>
              )}
              <StudentLinksList
                links={links}
                revokingId={revokingId}
                onRevoke={handleRevoke}
              />
            </section>
          </div>
        }
        sidebar={
          <>
            <Card data-testid="student-info">
              <h2 className="mb-3 font-serif text-base text-brand-slate-800">
                Details
              </h2>
              <dl className="space-y-2 text-sm">
                <div className="flex justify-between gap-4">
                  <dt className="text-brand-slate-500">Grade</dt>
                  <dd className="text-brand-slate-800">
                    {student.gradeLevel || "—"}
                  </dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-brand-slate-500">Disability</dt>
                  <dd className="text-right text-brand-slate-800">
                    {student.disabilityCategory || "—"}
                  </dd>
                </div>
              </dl>
            </Card>

            <Card>
              <h2 className="mb-2 font-serif text-base text-brand-slate-800">
                IEPs
              </h2>
              <p className="mb-3 text-sm text-brand-slate-600">
                Build and edit IEP drafts for this student.
              </p>
              <Link
                to={`/educator/students/${studentId}/iep-drafts`}
                data-testid="build-ieps"
              >
                <Button variant="secondary" className="w-full">
                  Build / view IEPs
                </Button>
              </Link>
            </Card>
          </>
        }
      />
    </PageLayout>
  );
}
