import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { apiErrorMessage } from "@/lib/api-error";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { PageLayout } from "@/components/ui/page-layout";
import { usePageTitle } from "@/hooks/use-page-title";
import { DetailLayout } from "@/components/ui/detail-layout";
import { getDistrictSchools } from "@/features/district-admin/api/district-api";
import type { DistrictSchool } from "@/features/district-admin/types";
import { ORG_ROLE, isAdminOrgRole } from "../types";
import type { StudentTeamMember } from "../types";
import { useEducatorProfile } from "../hooks/use-educator-profile";
import { useStudentRecord } from "../hooks/use-student-record";
import { FamilyLinksSection } from "../components/family-links-section";
import { StudentDetailsCard } from "../components/student-details-card";
import { StudentDocumentsSection } from "../components/student-documents-section";
import { EditStudentDrawer } from "../components/edit-student-drawer";
import { StudentLifecycleActions } from "../components/lifecycle/student-lifecycle-actions";
import { StudentTeamPanel } from "../components/team/student-team-panel";
import { InviteStudentForm } from "@/features/student/components/invite-student-form";
import { inviteStudentFromEducator } from "@/features/student/api/student-invite-api";
import { StudentMeetingsCard } from "@/features/meetings/components/student-meetings-card";
import { StudentTimelineCard } from "@/features/obligations/components/student-timeline-card";

export function EducatorStudentDetailPage() {
  const { studentId: studentIdParam } = useParams<{ studentId: string }>();
  const studentId = Number(studentIdParam);
  const record = useStudentRecord(studentId);
  const { student } = record;
  // Never the student's legal name — the tab title lands in browser history,
  // OS taskbar/Alt-Tab previews, and screen-share tab pickers, all reachable
  // by a bystander who never authenticated to the app. The full name stays in
  // the in-page heading only (see `PageLayout title={studentName}` below).
  usePageTitle("Student record");

  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [isInviteStudentOpen, setIsInviteStudentOpen] = useState(false);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [teamMembers, setTeamMembers] = useState<StudentTeamMember[] | null>(null);
  const { profile } = useEducatorProfile();
  const isAdmin = isAdminOrgRole(profile?.orgRoleId);
  const isDistrictAdmin = profile?.orgRoleId === ORG_ROLE.DistrictAdmin;
  // PUT requires Collaborator+; a Viewer-level member (LEA rep / interpreter
  // default) is not offered an Edit button it could only get a 403 from.
  const currentMember = teamMembers?.find((m) => m.userId === profile?.userId) ?? null;
  const canEdit = isAdmin || (currentMember !== null && currentMember.accessRole !== "Viewer");

  // DistrictAdmin needs the school list for Transfer.
  useEffect(() => {
    if (!isDistrictAdmin) return;
    let active = true;
    (async () => {
      try {
        const response = await getDistrictSchools();
        if (active && response.success && response.data) setSchools(response.data);
      } catch {
        if (active) setSchools([]);
      }
    })();
    return () => {
      active = false;
    };
  }, [isDistrictAdmin]);

  const handleInviteStudent = async (email: string) => {
    try {
      const response = await inviteStudentFromEducator(studentId, email);
      if (response.success) setIsInviteStudentOpen(false);
      return { success: response.success, message: response.message };
    } catch (err) {
      return { success: false, message: apiErrorMessage(err, "An error occurred sending the invitation") };
    }
  };

  if (record.isLoading) {
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
      actions={
        isAdmin ? (
          <StudentLifecycleActions
            student={student}
            schools={isDistrictAdmin ? schools : undefined}
            onExit={record.exit}
            onReactivate={record.reactivate}
            onArchive={record.archive}
            onTransfer={record.transfer}
          />
        ) : undefined
      }
    >
      <DetailLayout
        main={
          <div className="space-y-6">
            <StudentDocumentsSection studentId={studentId} />

            <StudentMeetingsCard studentId={studentId} studentName={studentName} />

            <section className="space-y-3">
              <h2 className="font-serif text-lg">IEP team</h2>
              <StudentTeamPanel
                studentId={studentId}
                studentSchoolId={student.schoolId}
                isAdmin={isAdmin}
                currentUserId={profile?.userId}
                onTeamChanged={record.reload}
                onMembersChange={setTeamMembers}
                family={<FamilyLinksSection studentId={studentId} />}
              />
            </section>
          </div>
        }
        sidebar={
          <>
            <StudentDetailsCard
              student={student}
              onEdit={canEdit ? () => setIsEditOpen(true) : undefined}
            />

            <StudentTimelineCard
              studentId={studentId}
              onEditDates={canEdit ? () => setIsEditOpen(true) : undefined}
            />

            <Card>
              <h2 className="mb-2 font-serif text-base text-brand-slate-800">
                Student account
              </h2>
              <p className="mb-3 text-sm text-brand-slate-600">
                Invite {student.firstName} to take part in their IEP process.
              </p>
              <Button
                variant="secondary"
                className="w-full"
                onClick={() => setIsInviteStudentOpen(true)}
                data-testid="invite-student-open"
              >
                Invite student
              </Button>
            </Card>
          </>
        }
      />

      <EditStudentDrawer
        open={isEditOpen}
        student={student}
        onClose={() => setIsEditOpen(false)}
        onSubmit={async (data) => {
          const result = await record.update(data);
          if (result.success) setIsEditOpen(false);
          return result;
        }}
      />

      <Modal
        open={isInviteStudentOpen}
        onClose={() => setIsInviteStudentOpen(false)}
        title="Invite student"
        data-testid="invite-student-modal"
      >
        <InviteStudentForm
          embedded
          onInvite={handleInviteStudent}
          description={`Invite ${student.firstName} to activate their own account and take part in their IEP process.`}
        />
      </Modal>
    </PageLayout>
  );
}
