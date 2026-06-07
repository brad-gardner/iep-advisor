import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { MainLayout } from '@/components/layouts/main-layout';
import { AuthLayout } from '@/components/layouts/auth-layout';
import { LoginPage } from '@/features/auth/components/login-page';
import { RegisterPage } from '@/features/auth/components/register-page';
import { DashboardPage } from '@/features/auth/components/dashboard-page';
import { ProfilePage } from '@/features/auth/components/profile-page';
import { MfaVerifyPage } from '@/features/auth/components/mfa-verify-page';
import { MfaSetupPage } from '@/features/auth/components/mfa-setup-page';
import { ForgotPasswordPage } from '@/features/auth/components/forgot-password-page';
import { ResetPasswordPage } from '@/features/auth/components/reset-password-page';
import { ChildrenListPage } from '@/features/children/components/children-list-page';
import { CreateChildPage } from '@/features/children/components/create-child-page';
import { ChildDetailPage } from '@/features/children/components/child-detail-page';
import { ChildOverviewTab } from '@/features/children/components/child-overview-tab';
import { ChildIepsTab } from '@/features/children/components/child-ieps-tab';
import { ChildEtrsTab } from '@/features/children/components/child-etrs-tab';
import { ChildGoalsTab } from '@/features/children/components/child-goals-tab';
import { ChildAnalysisTab } from '@/features/analysis/components/child-analysis-tab';
import { ChildMeetingPrepTab } from '@/features/meeting-prep/components/child-meeting-prep-tab';
import { IepViewerPage } from '@/features/iep-documents/components/iep-viewer-page';
import { IepRouteRedirect } from '@/features/iep-documents/components/iep-route-redirect';
import { ProgressReportViewerPage } from '@/features/progress-reports/components/progress-report-viewer-page';
import { EtrViewerPage } from '@/features/etr-documents/components/etr-viewer-page';
import { EtrRouteRedirect } from '@/features/etr-documents/components/etr-route-redirect';
import { EtrListPage } from '@/features/etr-documents/components/etr-list-page';
import { ComparisonPage } from '@/features/iep-comparison/components/comparison-page';
import { OnboardingFlow } from '@/features/onboarding/components/onboarding-flow';
import { Iep101Page } from '@/features/onboarding/components/iep-101-page';
import { AcceptInvitePage } from '@/features/auth/components/accept-invite-page';
import { SubscriptionPage } from '@/features/subscription/components/subscription-page';
import { RedeemInvitePage } from '@/features/subscription/components/redeem-invite-page';
import { SubscriptionSuccessPage } from '@/features/subscription/components/subscription-success-page';
import { SubscriptionCancelPage } from '@/features/subscription/components/subscription-cancel-page';
import { KnowledgeBasePage } from '@/features/knowledge-base/components/knowledge-base-page';
import { AdminRouteGuard } from '@/features/admin/components/admin-route-guard';
import { AdminDashboardPage } from '@/features/admin/components/admin-dashboard-page';
import { AdminUsersPage } from '@/features/admin/components/admin-users-page';
import { AdminUserDetail } from '@/features/admin/components/admin-user-detail';
import { EducatorHomePage } from '@/features/educator/pages/educator-home-page';
import { EducatorStudentsPage } from '@/features/educator/pages/educator-students-page';
import { EducatorStudentDetailPage } from '@/features/educator/pages/educator-student-detail-page';
import { DistrictSchoolsPage } from '@/features/district-admin/pages/district-schools-page';
import { DistrictSetupWizard } from '@/features/district-admin/pages/district-setup-wizard';
import { DistrictStaffPage } from '@/features/staff-invites/pages/district-staff-page';
import { StaffAcceptInvitePage } from '@/features/staff-invites/pages/staff-accept-invite-page';
import { IepDraftListPage } from '@/features/iep-authoring/pages/iep-draft-list-page';
import { IepAuthoringWorkspacePage } from '@/features/iep-authoring/pages/iep-authoring-workspace-page';
import { AcceptLinkPage } from '@/features/child-links/components/accept-link-page';
import { EducatorVersionDetailPage } from '@/features/iep-versions/components/educator-version-detail-page';
import { StudentHomePage } from '@/features/student/pages/student-home-page';
import { StudentAcceptInvitePage } from '@/features/student/components/student-accept-invite-page';
import { ParentVersionDetailPage } from '@/features/iep-versions/components/parent-version-detail-page';
import { RoleHome, RoleRoute } from '@/app/role-routing';
import { roleHome } from '@/app/role-home';

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-white"></div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

function PublicRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading, user } = useAuth();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-white"></div>
      </div>
    );
  }

  if (isAuthenticated && user) {
    // A page that auto-logs-in (e.g. district signup) can request a specific
    // post-auth destination; otherwise fall back to the role's default home.
    // Without this, persisting the session re-renders this guard and its
    // <Navigate> can clobber the page's own navigate() (e.g. to the setup
    // wizard) depending on commit order. Read-only here — the destination page
    // clears the key once it has mounted, so repeated renders of this guard stay
    // consistent.
    const requested = sessionStorage.getItem('post-auth-redirect');
    return <Navigate to={requested ?? roleHome(user.role)} replace />;
  }

  return <>{children}</>;
}

export function AppRouter() {
  return (
    <Routes>
      <Route
        path="/login"
        element={
          <PublicRoute>
            <AuthLayout>
              <LoginPage />
            </AuthLayout>
          </PublicRoute>
        }
      />
      <Route
        path="/register"
        element={
          <PublicRoute>
            <AuthLayout>
              <RegisterPage />
            </AuthLayout>
          </PublicRoute>
        }
      />
      <Route
        path="/forgot-password"
        element={
          <PublicRoute>
            <AuthLayout>
              <ForgotPasswordPage />
            </AuthLayout>
          </PublicRoute>
        }
      />
      <Route
        path="/reset-password"
        element={
          <PublicRoute>
            <AuthLayout>
              <ResetPasswordPage />
            </AuthLayout>
          </PublicRoute>
        }
      />
      {/* Bare route (no PublicRoute): an authenticated user is NOT bounced —
          they get a "sign out to continue" prompt on the accept page. */}
      <Route
        path="/staff/accept-invite"
        element={
          <AuthLayout>
            <StaffAcceptInvitePage />
          </AuthLayout>
        }
      />
      <Route path="/mfa-verify" element={<MfaVerifyPage />} />
      <Route
        path="/onboarding"
        element={
          <ProtectedRoute>
            <OnboardingFlow />
          </ProtectedRoute>
        }
      />
      <Route
        path="/dashboard"
        element={
          <ProtectedRoute>
            <MainLayout>
              <DashboardPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/children"
        element={
          <ProtectedRoute>
            <MainLayout>
              <ChildrenListPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/children/new"
        element={
          <ProtectedRoute>
            <MainLayout>
              <CreateChildPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/children/:childId"
        element={
          <ProtectedRoute>
            <MainLayout>
              <ChildDetailPage />
            </MainLayout>
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="overview" replace />} />
        <Route path="overview" element={<ChildOverviewTab />} />
        <Route path="ieps" element={<ChildIepsTab />} />
        <Route path="etrs" element={<ChildEtrsTab />} />
        <Route path="goals" element={<ChildGoalsTab />} />
        <Route path="analysis" element={<ChildAnalysisTab />} />
        <Route path="meeting-prep" element={<ChildMeetingPrepTab />} />
      </Route>
      <Route
        path="/children/:childId/ieps/:id"
        element={
          <ProtectedRoute>
            <MainLayout>
              <IepViewerPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/children/:childId/ieps/:id/progress-reports/:prId"
        element={
          <ProtectedRoute>
            <MainLayout>
              <ProgressReportViewerPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/children/:childId/etrs/:id"
        element={
          <ProtectedRoute>
            <MainLayout>
              <EtrViewerPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/children/:childId/compare/:iepId/:otherId"
        element={
          <ProtectedRoute>
            <MainLayout>
              <ComparisonPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/ieps/:id"
        element={
          <ProtectedRoute>
            <MainLayout>
              <IepRouteRedirect />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/etrs"
        element={
          <ProtectedRoute>
            <MainLayout>
              <EtrListPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/etrs/:id"
        element={
          <ProtectedRoute>
            <MainLayout>
              <EtrRouteRedirect />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/profile"
        element={
          <ProtectedRoute>
            <MainLayout>
              <ProfilePage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/mfa-setup"
        element={
          <ProtectedRoute>
            <MainLayout>
              <MfaSetupPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/knowledge-base"
        element={
          <ProtectedRoute>
            <MainLayout>
              <KnowledgeBasePage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/iep-101"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Iep101Page />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/accept-invite"
        element={
          <ProtectedRoute>
            <MainLayout>
              <AcceptInvitePage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      {/* Educator shell — Educator-only. Parents/Students are bounced to their
          own home by RoleRoute (onboarding-via-/educator was removed in P5). */}
      <Route
        path="/educator"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <EducatorHomePage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      {/* Full-screen first-run wizard (own chrome, no MainLayout) — like
          /onboarding. The page itself guards to DistrictAdmin. */}
      <Route
        path="/educator/setup"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <DistrictSetupWizard />
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/admin/schools"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <DistrictSchoolsPage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/admin/staff"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <DistrictStaffPage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/students"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <EducatorStudentsPage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/students/:studentId"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <EducatorStudentDetailPage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/students/:studentId/iep-drafts"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <IepDraftListPage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/students/:studentId/iep-drafts/:draftId"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <IepAuthoringWorkspacePage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/students/:studentId/iep-versions/:versionId"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <EducatorVersionDetailPage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      {/* Parent surface (unconditional now) — finalized version the school
          shared for this child. */}
      <Route
        path="/children/:childId/iep-versions/:versionId"
        element={
          <ProtectedRoute>
            <MainLayout>
              <ParentVersionDetailPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      {/* Student shell — Student-only. */}
      <Route
        path="/student"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Student']}>
              <MainLayout>
                <StudentHomePage />
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      {/* Accepting a student invite flips the user to the Student role, so the
          invitee is typically a Parent (or freshly-converted user) when they
          land here — NO Student RoleRoute, or they'd be bounced before they can
          accept. Auth is still required (ProtectedRoute). */}
      <Route
        path="/student/accept-invite"
        element={
          <ProtectedRoute>
            <MainLayout>
              <StudentAcceptInvitePage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      {/* Parent accepting a school link — auth required, any role; no role gate. */}
      <Route
        path="/accept-link"
        element={
          <ProtectedRoute>
            <MainLayout>
              <AcceptLinkPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/subscription"
        element={
          <ProtectedRoute>
            <MainLayout>
              <SubscriptionPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/subscription/success"
        element={
          <ProtectedRoute>
            <MainLayout>
              <SubscriptionSuccessPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/subscription/cancel"
        element={
          <ProtectedRoute>
            <MainLayout>
              <SubscriptionCancelPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/redeem-invite"
        element={
          <ProtectedRoute>
            <MainLayout>
              <RedeemInvitePage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin"
        element={
          <ProtectedRoute>
            <MainLayout>
              <AdminRouteGuard>
                <AdminDashboardPage />
              </AdminRouteGuard>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/users"
        element={
          <ProtectedRoute>
            <MainLayout>
              <AdminRouteGuard>
                <AdminUsersPage />
              </AdminRouteGuard>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/users/:id"
        element={
          <ProtectedRoute>
            <MainLayout>
              <AdminRouteGuard>
                <AdminUserDetail />
              </AdminRouteGuard>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route path="/" element={<RoleHome />} />
      <Route path="*" element={<RoleHome />} />
    </Routes>
  );
}
