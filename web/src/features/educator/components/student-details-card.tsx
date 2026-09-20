import { Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { formatDate } from '@/lib/format-date';
import {
  DISABILITY_CATEGORY_LABELS,
  EXIT_REASON_LABELS,
  GRADE_LEVEL_LABELS,
} from '../types';
import type { SchoolStudent } from '../types';
import { StudentStatusBadge } from './student-status-badge';

interface StudentDetailsCardProps {
  student: SchoolStudent;
  onEdit?: () => void;
}

function Row({ label, children, testId }: { label: string; children: React.ReactNode; testId?: string }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="shrink-0 text-brand-slate-500">{label}</dt>
      <dd className="text-right text-brand-slate-800" data-testid={testId}>
        {children}
      </dd>
    </div>
  );
}

// Sidebar summary of the student record: identity, placement, lifecycle and
// the IEP/ETR timeline dates. Read-only; editing happens in the Edit drawer.
export function StudentDetailsCard({ student, onEdit }: StudentDetailsCardProps) {
  const disability = student.disabilityCategory
    ? DISABILITY_CATEGORY_LABELS[student.disabilityCategory]
    : '—';

  return (
    <Card data-testid="student-info">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-serif text-base text-brand-slate-800">Details</h2>
        {onEdit && (
          <Button variant="ghost" size="sm" onClick={onEdit} data-testid="student-edit-open">
            <Pencil className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
            Edit
          </Button>
        )}
      </div>

      <dl className="space-y-2 text-sm">
        <Row label="Status" testId="student-info-status">
          <StudentStatusBadge status={student.status} />
        </Row>
        {student.status === 'Exited' && (
          <Row label="Exited" testId="student-info-exit">
            {student.exitReason ? EXIT_REASON_LABELS[student.exitReason] : '—'}
            {student.exitedAt ? ` · ${formatDate(student.exitedAt)}` : ''}
          </Row>
        )}
        <Row label="Student ID" testId="student-info-external-id">
          {student.externalStudentId || '—'}
        </Row>
        {student.schoolName && <Row label="School">{student.schoolName}</Row>}
        <Row label="Date of birth" testId="student-info-dob">
          {formatDate(student.dateOfBirth)}
        </Row>
        <Row label="Grade" testId="student-info-grade">
          {student.gradeLevel ? GRADE_LEVEL_LABELS[student.gradeLevel] : '—'}
        </Row>
        <Row label="Disability" testId="student-info-disability">
          {disability}
          {student.legacyDisabilityText && (
            <span className="block text-xs text-brand-slate-500">
              Previously: {student.legacyDisabilityText}
            </span>
          )}
        </Row>
        <Row label="Home language">{student.homeLanguage || '—'}</Row>
        <Row label="Case manager" testId="student-info-case-manager">
          {student.caseManagerName || 'None'}
        </Row>
      </dl>

      <h3 className="mb-2 mt-4 text-xs font-medium uppercase tracking-wide text-brand-slate-500">
        Timeline
      </h3>
      <dl className="space-y-2 text-sm" data-testid="student-info-timeline">
        <Row label="IEP date">{formatDate(student.iepDate)}</Row>
        <Row label="Annual review due">{formatDate(student.annualReviewDueDate)}</Row>
        <Row label="ETR date">{formatDate(student.etrDate)}</Row>
        <Row label="Reevaluation due">{formatDate(student.reevaluationDueDate)}</Row>
      </dl>
    </Card>
  );
}
