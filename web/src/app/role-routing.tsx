import { Navigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/hooks/use-auth';
import type { UserRole } from '@/types/api';
import { roleHome } from './role-home';

// Redirects an authenticated user to their role's home. Used for `/` and the
// catch-all so a freshly-converted educator lands in the right place.
export function RoleHome() {
  const { user } = useAuth();
  return <Navigate to={user ? roleHome(user.role) : '/dashboard'} replace />;
}

// Gates a route to a set of roles, redirecting others to their own home. Used
// by the educator and student shells: a Parent hitting /educator/* is bounced
// to /dashboard, an Educator hitting /student/* to /educator, etc.
export function RoleRoute({
  allow,
  children,
}: {
  allow: UserRole[];
  children: React.ReactNode;
}) {
  const { user } = useAuth();
  if (user && !allow.includes(user.role)) {
    return <Navigate to={roleHome(user.role)} replace />;
  }
  return <>{children}</>;
}
