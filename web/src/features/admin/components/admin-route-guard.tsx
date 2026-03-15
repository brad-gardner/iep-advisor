import { Navigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/hooks/use-auth';

interface AdminRouteGuardProps {
  children: React.ReactNode;
}

export function AdminRouteGuard({ children }: AdminRouteGuardProps) {
  const { user } = useAuth();

  if (user?.role !== 'Admin') {
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
}
