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
import { IepViewerPage } from '@/features/iep-documents/components/iep-viewer-page';
import { ComparisonPage } from '@/features/iep-comparison/components/comparison-page';
import { OnboardingFlow } from '@/features/onboarding/components/onboarding-flow';
import { Iep101Page } from '@/features/onboarding/components/iep-101-page';

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
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-white"></div>
      </div>
    );
  }

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
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
        path="/children/:id"
        element={
          <ProtectedRoute>
            <MainLayout>
              <ChildDetailPage />
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
              <IepViewerPage />
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
        path="/iep-101"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Iep101Page />
            </MainLayout>
          </ProtectedRoute>
        }
      />
      <Route path="/" element={<Navigate to="/dashboard" replace />} />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}
