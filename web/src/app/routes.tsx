import { lazy, Suspense, useEffect } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { warmRichTextEditor } from '@/components/ui/rich-text-editor';
import { MainLayout } from '@/components/layouts/main-layout';
import { AuthLayout } from '@/components/layouts/auth-layout';
import { LoginPage } from '@/features/auth/components/login-page';
import { RegisterPage } from '@/features/auth/components/register-page';
import { ParentHomePage } from '@/features/home/pages/parent-home-page';
import { ProfilePage } from '@/features/auth/components/profile-page';
import { MfaVerifyPage } from '@/features/auth/components/mfa-verify-page';
import { MfaSetupPage } from '@/features/auth/components/mfa-setup-page';
import { ForgotPasswordPage } from '@/features/auth/components/forgot-password-page';
import { ResetPasswordPage } from '@/features/auth/components/reset-password-page';
import { ChildrenListPage } from '@/features/children/components/children-list-page';
import { ChildDetailPage } from '@/features/children/components/child-detail-page';
import { ChildOverviewTab } from '@/features/children/components/child-overview-tab';
import { ChildIepsTab } from '@/features/children/components/child-ieps-tab';
import { ChildEtrsTab } from '@/features/children/components/child-etrs-tab';
import { ChildGoalsTab } from '@/features/children/components/child-goals-tab';
import { ChildAnalysisTab } from '@/features/analysis/components/child-analysis-tab';
import { ChildMeetingPrepTab } from '@/features/meeting-prep/components/child-meeting-prep-tab';
import { JournalPage } from '@/features/journal/components/journal-page';
// The advocate page (chat + streaming) is code-split so the main chunk does not
// carry it for the many pages that never open it.
const AdvocatePage = lazy(() => import('@/features/advocate/components/advocate-page'));
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
import { StaffAcceptInvitePage } from '@/features/staff-invites/pages/staff-accept-invite-page';
import { AcceptLinkPage } from '@/features/child-links/components/accept-link-page';
import { StudentHomePage } from '@/features/student/pages/student-home-page';
import { StudentAcceptInvitePage } from '@/features/student/components/student-accept-invite-page';
import { ParentVersionDetailPage } from '@/features/iep-versions/components/parent-version-detail-page';
import { ParentAuthoredVersionPage } from '@/features/document-authoring/pages/parent-authored-version-page';
import { SharedDraftsListPage } from '@/features/shared-drafts/pages/shared-drafts-list-page';
import { SharedDraftReviewPage } from '@/features/shared-drafts/pages/shared-draft-review-page';
import { MeetingSummaryPage } from '@/features/shared-drafts/pages/meeting-summary-page';
import { RoleHome, RoleRoute } from '@/app/role-routing';
import { roleHome } from '@/app/role-home';
import { Spinner } from '@/components/ui/spinner';
import { NotificationsPage } from '@/features/notifications/pages/notifications-page';
import { MeetingRsvpPage } from '@/features/meetings/pages/meeting-rsvp-page';
import { MagicLinkConsumePage } from '@/features/auth/components/magic-link-consume-page';
import { CancelDeletionPage } from '@/features/auth/components/cancel-deletion-page';

// Educator ("staff") area — `/educator/*`, minus the district-admin pages
// under `/educator/admin/*` (their own chunk below). One `import()`
// specifier shared by every page here so Vite bundles them as a single
// chunk (multilingual plan, phase 5 — "one chunk per area is fine"); see
// `app/lazy-routes/staff-routes.tsx`'s doc comment for why, and for the
// `educator` namespace's English registration this chunk carries with it.
const StaffHomePage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.StaffHomePage }))
);
const EducatorStudentsPage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.EducatorStudentsPage }))
);
const EducatorStudentDetailPage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.EducatorStudentDetailPage }))
);
const EducatorCalendarPage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.EducatorCalendarPage }))
);
const MeetingBriefPage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.MeetingBriefPage }))
);
const DocumentListPage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.DocumentListPage }))
);
const DocumentEditorPage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.DocumentEditorPage }))
);
const AuthoredVersionDetailPage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.AuthoredVersionDetailPage }))
);
const EducatorVersionDetailPage = lazy(() =>
  import('@/app/lazy-routes/staff-routes').then((m) => ({ default: m.EducatorVersionDetailPage }))
);

// District-admin — `/educator/admin/*` plus the district first-run wizard at
// `/educator/setup`. Its own chunk, separate from the staff area above; see
// `app/lazy-routes/district-admin-routes.tsx`.
const DistrictSchoolsPage = lazy(() =>
  import('@/app/lazy-routes/district-admin-routes').then((m) => ({ default: m.DistrictSchoolsPage }))
);
const ComplianceBoardPage = lazy(() =>
  import('@/app/lazy-routes/district-admin-routes').then((m) => ({ default: m.ComplianceBoardPage }))
);
const DistrictAuditLogPage = lazy(() =>
  import('@/app/lazy-routes/district-admin-routes').then((m) => ({ default: m.DistrictAuditLogPage }))
);
const DistrictSetupWizard = lazy(() =>
  import('@/app/lazy-routes/district-admin-routes').then((m) => ({ default: m.DistrictSetupWizard }))
);
const DistrictStaffPage = lazy(() =>
  import('@/app/lazy-routes/district-admin-routes').then((m) => ({ default: m.DistrictStaffPage }))
);
const ImportPage = lazy(() =>
  import('@/app/lazy-routes/district-admin-routes').then((m) => ({ default: m.ImportPage }))
);
const ExportsAdminPage = lazy(() =>
  import('@/app/lazy-routes/district-admin-routes').then((m) => ({ default: m.ExportsAdminPage }))
);

// Platform admin — `/admin/*`. Its own chunk; see
// `app/lazy-routes/platform-admin-routes.tsx`.
const AdminRouteGuard = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.AdminRouteGuard }))
);
const AdminDashboardPage = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.AdminDashboardPage }))
);
const AdminUsersPage = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.AdminUsersPage }))
);
const AdminUserDetail = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.AdminUserDetail }))
);
const TemplateListPage = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.TemplateListPage }))
);
const TemplateBuilderPage = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.TemplateBuilderPage }))
);
const AdminNotificationFailuresPage = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.AdminNotificationFailuresPage }))
);
const AdminEmailPage = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.AdminEmailPage }))
);
const AdminAuditPage = lazy(() =>
  import('@/app/lazy-routes/platform-admin-routes').then((m) => ({ default: m.AdminAuditPage }))
);

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Spinner tone="current" className="text-white" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <WarmEditorOnIdle>{children}</WarmEditorOnIdle>;
}

// Every signed-in surface can reach a rich text field (notes, questions,
// document narratives), so warm the editor once the first protected page is
// idle rather than on the first field someone opens. Public pages never
// trigger the download.
function WarmEditorOnIdle({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    warmRichTextEditor();
  }, []);
  return <>{children}</>;
}

function PublicRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading, user } = useAuth();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Spinner tone="current" className="text-white" />
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
  const { t } = useTranslation('common');
  // Shared fallback for every lazy staff/district-admin/platform-admin route
  // chunk below — same `justify-center py-12` in-page loading treatment used
  // elsewhere in the app (e.g. `journal-page.tsx`, `iep-viewer-page.tsx`),
  // rather than a full-screen spinner, since these render inside MainLayout's
  // already-visible chrome. Reuses the existing generic `common:ui.loading`
  // label rather than adding a new per-area key (`docs/i18n/README.md`'s
  // "Generic loading… text" rule) — the Advocate route below keeps its own
  // more specific fallback text.
  const lazyRouteFallback = (
    <div className="flex justify-center py-12">
      <Spinner label={t('ui.loading')} />
    </div>
  );
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
      {/* Public, unauthenticated: linked from the meeting invitation email.
          No ProtectedRoute/PublicRoute — works whether or not the visitor is
          logged in, own chrome (no MainLayout/AuthLayout). */}
      <Route path="/meetings/rsvp" element={<MeetingRsvpPage />} />
      {/* Magic-link sign-in (pilot-gates plan, phase 3). Bare like
          /staff/accept-invite — works regardless of any existing session,
          since consuming the link is meant to establish a fresh one. */}
      <Route
        path="/auth/magic"
        element={
          <AuthLayout>
            <MagicLinkConsumePage />
          </AuthLayout>
        }
      />
      {/* Public, unauthenticated: linked from the deletion-request email.
          Own chrome, no MainLayout/AuthLayout — mirrors /meetings/rsvp. */}
      <Route path="/account/cancel-deletion" element={<CancelDeletionPage />} />
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
              <ParentHomePage />
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
        {/* Parent-private dated journal — a child sub-page with its own tab. */}
        <Route path="journal" element={<JournalPage />} />
        {/* Parent-private Virtual Advocate chat — same layout, lazy chunk. */}
        <Route
          path="advocate"
          element={
            <Suspense
              fallback={
                <div className="flex justify-center py-12">
                  <Spinner label={t('routes.loadingAdvocate')} />
                </div>
              }
            >
              <AdvocatePage />
            </Suspense>
          }
        />
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
      {/* All roles — no RoleRoute gate. */}
      <Route
        path="/notifications"
        element={
          <ProtectedRoute>
            <MainLayout>
              <NotificationsPage />
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
      {/* One entry brought into view (advocate "Sources" chips link here). */}
      <Route
        path="/knowledge-base/:entryId"
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
                <Suspense fallback={lazyRouteFallback}>
                  <StaffHomePage />
                </Suspense>
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
              <Suspense fallback={lazyRouteFallback}>
                <DistrictSetupWizard />
              </Suspense>
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
                <Suspense fallback={lazyRouteFallback}>
                  <DistrictSchoolsPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/admin/compliance"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <Suspense fallback={lazyRouteFallback}>
                  <ComplianceBoardPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/admin/exports"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <Suspense fallback={lazyRouteFallback}>
                  <ExportsAdminPage />
                </Suspense>
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
                <Suspense fallback={lazyRouteFallback}>
                  <DistrictStaffPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/admin/activity"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <Suspense fallback={lazyRouteFallback}>
                  <DistrictAuditLogPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/admin/imports"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <Suspense fallback={lazyRouteFallback}>
                  <ImportPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/calendar"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <Suspense fallback={lazyRouteFallback}>
                  <EducatorCalendarPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/meetings/:meetingId/brief"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <Suspense fallback={lazyRouteFallback}>
                  <MeetingBriefPage />
                </Suspense>
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
                <Suspense fallback={lazyRouteFallback}>
                  <EducatorStudentsPage />
                </Suspense>
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
                <Suspense fallback={lazyRouteFallback}>
                  <EducatorStudentDetailPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/students/:studentId/documents"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <Suspense fallback={lazyRouteFallback}>
                  <DocumentListPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/documents/:instanceId"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              {/* Wide: the editor's own nav rail + content column need more
                  room than the standard page shell affords (plan
                  2026-10-02-002) — wraps the back-link, tabs AND editor
                  together (DocumentEditorPage's own outer div) so they align. */}
              <MainLayout wide>
                <Suspense fallback={lazyRouteFallback}>
                  <DocumentEditorPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      <Route
        path="/educator/students/:studentId/authored-versions/:versionId"
        element={
          <ProtectedRoute>
            <RoleRoute allow={['Educator']}>
              <MainLayout>
                <Suspense fallback={lazyRouteFallback}>
                  <AuthoredVersionDetailPage />
                </Suspense>
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
                <Suspense fallback={lazyRouteFallback}>
                  <EducatorVersionDetailPage />
                </Suspense>
              </MainLayout>
            </RoleRoute>
          </ProtectedRoute>
        }
      />
      {/* Parent surface — finalized documents the school shared for this child.
          Legacy typed IEP versions and template-authored versions both stay
          readable; new finalizes only produce the latter. */}
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
      <Route
        path="/children/:childId/authored-versions/:versionId"
        element={
          <ProtectedRoute>
            <MainLayout>
              <ParentAuthoredVersionPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      {/* Parent surface — plan 6: draft sharing/review, and post-meeting summaries. */}
      <Route
        path="/children/:childId/shared-drafts"
        element={
          <ProtectedRoute>
            <MainLayout>
              <SharedDraftsListPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/children/:childId/shared-drafts/:rev"
        element={
          <ProtectedRoute>
            <MainLayout>
              <SharedDraftReviewPage />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/children/:childId/meetings/:meetingId/summary"
        element={
          <ProtectedRoute>
            <MainLayout>
              <MeetingSummaryPage />
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
              <Suspense fallback={lazyRouteFallback}>
                <AdminRouteGuard>
                  <AdminDashboardPage />
                </AdminRouteGuard>
              </Suspense>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/users"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Suspense fallback={lazyRouteFallback}>
                <AdminRouteGuard>
                  <AdminUsersPage />
                </AdminRouteGuard>
              </Suspense>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/users/:id"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Suspense fallback={lazyRouteFallback}>
                <AdminRouteGuard>
                  <AdminUserDetail />
                </AdminRouteGuard>
              </Suspense>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/templates"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Suspense fallback={lazyRouteFallback}>
                <AdminRouteGuard>
                  <TemplateListPage />
                </AdminRouteGuard>
              </Suspense>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/templates/:templateId"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Suspense fallback={lazyRouteFallback}>
                <AdminRouteGuard>
                  <TemplateBuilderPage />
                </AdminRouteGuard>
              </Suspense>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/notifications"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Suspense fallback={lazyRouteFallback}>
                <AdminRouteGuard>
                  <AdminNotificationFailuresPage />
                </AdminRouteGuard>
              </Suspense>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/email"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Suspense fallback={lazyRouteFallback}>
                <AdminRouteGuard>
                  <AdminEmailPage />
                </AdminRouteGuard>
              </Suspense>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/audit"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Suspense fallback={lazyRouteFallback}>
                <AdminRouteGuard>
                  <AdminAuditPage />
                </AdminRouteGuard>
              </Suspense>
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route path="/" element={<RoleHome />} />
      <Route path="*" element={<RoleHome />} />
    </Routes>
  );
}
