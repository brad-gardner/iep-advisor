import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { useHome } from '../hooks/use-home';
import { AdminHome } from './admin-home';
import { CaseloadHome } from './caseload-home';

/** Fetches the single `/api/home` aggregate and dispatches to the right
 * variant body. A load failure always renders an error notice with retry —
 * never an empty state. */
export function StaffHomeBody() {
  const { home, isLoading, error, retry } = useHome();

  if (isLoading) {
    return (
      <div className="space-y-6" data-testid="staff-home-loading">
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (error || !home || home.kind !== 'Staff' || !home.staff) {
    return (
      <Card data-testid="staff-home-error">
        <Notice variant="error" title={error ?? "Couldn't load your home"}>
          <Button variant="secondary" className="mt-2" onClick={retry} data-testid="staff-home-retry">
            Try again
          </Button>
        </Notice>
      </Card>
    );
  }

  const { staff, generatedAt } = home;
  if (staff.variant === 'SchoolAdmin' || staff.variant === 'DistrictAdmin') {
    return (
      <AdminHome staff={staff} isDistrict={staff.variant === 'DistrictAdmin'} generatedAt={generatedAt} />
    );
  }
  return <CaseloadHome staff={staff} />;
}
