import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';
import type { DeactivateStaffResponse } from '../types';

interface DeactivateSolelyOwnedNoticeProps {
  result: DeactivateStaffResponse;
  onDismiss: () => void;
}

// Shown after a deactivate when the staff member solely owned one or more
// students — prompts an admin to reassign them from each student's page.
export function DeactivateSolelyOwnedNotice({
  result,
  onDismiss,
}: DeactivateSolelyOwnedNoticeProps) {
  const { t } = useTranslation('staff-invites');
  const { solelyOwnedStudentCount, solelyOwnedStudents } = result;

  return (
    <div data-testid="staff-deactivate-solely-owned">
      <Notice
        variant="warning"
        title={t('deactivateSolelyOwnedNotice.title', { count: solelyOwnedStudentCount })}
      >
        <ul className="mt-1 space-y-1">
          {solelyOwnedStudents.map((student) => (
            <li key={student.studentId}>
              <Link
                to={`/educator/students/${student.studentId}`}
                className="text-brand-teal-600 hover:underline"
                data-testid={`staff-deactivate-solely-owned-student-${student.studentId}`}
              >
                {student.name}
              </Link>
            </li>
          ))}
        </ul>
        <div className="mt-3">
          <Button
            variant="ghost"
            onClick={onDismiss}
            data-testid="staff-deactivate-solely-owned-dismiss"
          >
            {t('deactivateSolelyOwnedNotice.dismiss')}
          </Button>
        </div>
      </Notice>
    </div>
  );
}
