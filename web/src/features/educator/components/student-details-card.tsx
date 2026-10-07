import { Pencil } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { disabilityCategoryLabel } from '@/lib/disability-category-label';
import { formatDate } from '@/lib/format-date';
import { gradeLevelLabel } from '@/lib/grade-level-label';
import { exitReasonLabel } from '../lib/student-enum-labels';
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
  const { t } = useTranslation(['educator', 'common']);
  const disability = student.disabilityCategory
    ? disabilityCategoryLabel(student.disabilityCategory)
    : t('common:ui.notSet');

  return (
    <Card data-testid="student-info">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-serif text-base text-brand-slate-800">{t('educator:studentDetailsCard.title')}</h2>
        {onEdit && (
          <Button variant="ghost" size="sm" onClick={onEdit} data-testid="student-edit-open">
            <Pencil className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
            {t('educator:studentDetailsCard.edit')}
          </Button>
        )}
      </div>

      <dl className="space-y-2 text-sm">
        <Row label={t('educator:studentDetailsCard.statusLabel')} testId="student-info-status">
          <StudentStatusBadge status={student.status} />
        </Row>
        {student.status === 'Exited' && (
          <Row label={t('educator:studentDetailsCard.exitedLabel')} testId="student-info-exit">
            {student.exitReason ? exitReasonLabel(student.exitReason) : t('common:ui.notSet')}
            {student.exitedAt ? ` · ${formatDate(student.exitedAt)}` : ''}
          </Row>
        )}
        <Row label={t('educator:studentDetailsCard.studentIdLabel')} testId="student-info-external-id">
          {student.externalStudentId || t('common:ui.notSet')}
        </Row>
        {student.schoolName && <Row label={t('educator:studentDetailsCard.schoolLabel')}>{student.schoolName}</Row>}
        <Row label={t('educator:studentDetailsCard.dobLabel')} testId="student-info-dob">
          {formatDate(student.dateOfBirth)}
        </Row>
        <Row label={t('educator:studentDetailsCard.gradeLabel')} testId="student-info-grade">
          {student.gradeLevel ? gradeLevelLabel(student.gradeLevel) : t('common:ui.notSet')}
        </Row>
        <Row label={t('educator:studentDetailsCard.disabilityLabel')} testId="student-info-disability">
          {disability}
          {student.legacyDisabilityText && (
            <span className="block text-xs text-brand-slate-500">
              {t('educator:studentDetailsCard.previously', { text: student.legacyDisabilityText })}
            </span>
          )}
        </Row>
        <Row label={t('educator:studentDetailsCard.homeLanguageLabel')}>
          {student.homeLanguage || t('common:ui.notSet')}
        </Row>
        <Row label={t('educator:studentDetailsCard.caseManagerLabel')} testId="student-info-case-manager">
          {student.caseManagerName || t('educator:studentDetailsCard.none')}
        </Row>
      </dl>

      <h3 className="mb-2 mt-4 text-xs font-medium uppercase tracking-wide text-brand-slate-500">
        {t('educator:studentDetailsCard.timelineHeading')}
      </h3>
      <dl className="space-y-2 text-sm" data-testid="student-info-timeline">
        <Row label={t('educator:studentDetailsCard.iepDateLabel')}>{formatDate(student.iepDate)}</Row>
        <Row label={t('educator:studentDetailsCard.annualReviewLabel')}>
          {formatDate(student.annualReviewDueDate)}
        </Row>
        <Row label={t('educator:studentDetailsCard.etrDateLabel')}>{formatDate(student.etrDate)}</Row>
        <Row label={t('educator:studentDetailsCard.reevaluationLabel')}>
          {formatDate(student.reevaluationDueDate)}
        </Row>
      </dl>
    </Card>
  );
}
