import type { UserRole } from '@/types/api';

// The landing route for a given role after login. Educators go to their app
// shell; everyone else (Parent/Student/Admin) goes to the parent dashboard.
export function roleHome(role: UserRole): string {
  return role === 'Educator' ? '/educator' : '/dashboard';
}
