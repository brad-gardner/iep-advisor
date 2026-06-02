import type { UserRole } from '@/types/api';

// The landing route for a given role after login. Educators go to their app
// shell, Students to theirs; everyone else (Parent/Admin) goes to the parent
// dashboard.
export function roleHome(role: UserRole): string {
  if (role === 'Educator') return '/educator';
  if (role === 'Student') return '/student';
  return '/dashboard';
}
