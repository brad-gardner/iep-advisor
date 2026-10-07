import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Archive, ArrowRightLeft, LogOut, RotateCcw } from 'lucide-react';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Menu, type MenuItem } from '@/components/ui/menu';
import type { DistrictSchool } from '@/features/district-admin/types';
import type { ExitStudentRequest, SchoolStudent, TransferStudentRequest } from '../../types';
import { ExitStudentModal } from './exit-student-modal';
import { TransferStudentModal } from './transfer-student-modal';

type ActionResult = Promise<{ success: boolean; error?: string }>;

interface StudentLifecycleActionsProps {
  student: SchoolStudent;
  // DistrictAdmin only: enables Transfer and supplies the target schools.
  schools?: DistrictSchool[];
  onExit: (data: ExitStudentRequest) => ActionResult;
  onReactivate: () => ActionResult;
  onArchive: () => ActionResult;
  onTransfer: (data: TransferStudentRequest) => ActionResult;
}

// Admin-only lifecycle verbs behind one header Menu. Each destructive verb
// confirms first; the server message on failure stays inside the dialog.
export function StudentLifecycleActions({
  student,
  schools,
  onExit,
  onReactivate,
  onArchive,
  onTransfer,
}: StudentLifecycleActionsProps) {
  const { t } = useTranslation('educator');
  const [isExitOpen, setIsExitOpen] = useState(false);
  const [isArchiveOpen, setIsArchiveOpen] = useState(false);
  const [isReactivateOpen, setIsReactivateOpen] = useState(false);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);

  const studentName = `${student.firstName} ${student.lastName ?? ''}`.trim();
  const icon = (Icon: typeof Archive) => <Icon className="h-3.5 w-3.5" strokeWidth={1.8} />;

  const items: MenuItem[] = [];
  if (student.status === 'Active') {
    items.push({
      label: t('lifecycleActions.exit'),
      icon: icon(LogOut),
      onSelect: () => setIsExitOpen(true),
      'data-testid': 'student-action-exit',
    });
  } else {
    items.push({
      label: t('lifecycleActions.reactivate'),
      icon: icon(RotateCcw),
      onSelect: () => {
        setDialogError(null);
        setIsReactivateOpen(true);
      },
      'data-testid': 'student-action-reactivate',
    });
  }
  if (schools) {
    items.push({
      label: t('lifecycleActions.transfer'),
      icon: icon(ArrowRightLeft),
      onSelect: () => setIsTransferOpen(true),
      'data-testid': 'student-action-transfer',
    });
  }
  if (student.status !== 'Archived') {
    items.push({
      label: t('lifecycleActions.archive'),
      icon: icon(Archive),
      variant: 'danger',
      onSelect: () => {
        setDialogError(null);
        setIsArchiveOpen(true);
      },
      'data-testid': 'student-action-archive',
    });
  }

  const runConfirmed = async (action: () => ActionResult, close: () => void) => {
    setIsBusy(true);
    setDialogError(null);
    const result = await action();
    if (result.success) close();
    else setDialogError(result.error ?? t('lifecycleActions.genericError'));
    setIsBusy(false);
  };

  return (
    <>
      <Menu
        label={t('lifecycleActions.ariaLabel', { name: studentName })}
        items={items}
        triggerClassName="inline-flex items-center rounded-button border-[1.5px] border-brand-slate-200 px-3 py-2 text-[13px] font-medium text-brand-slate-600 hover:bg-brand-slate-50"
        trigger={<span>{t('lifecycleActions.trigger')}</span>}
        data-testid="student-actions-menu"
      />

      <ExitStudentModal
        open={isExitOpen}
        studentName={studentName}
        onClose={() => setIsExitOpen(false)}
        onSubmit={async (data) => {
          const result = await onExit(data);
          if (result.success) setIsExitOpen(false);
          return result;
        }}
      />

      {schools && (
        <TransferStudentModal
          open={isTransferOpen}
          studentName={studentName}
          currentSchoolId={student.schoolId}
          schools={schools}
          onClose={() => setIsTransferOpen(false)}
          onSubmit={async (data) => {
            const result = await onTransfer(data);
            if (result.success) setIsTransferOpen(false);
            return result;
          }}
        />
      )}

      <ConfirmDialog
        open={isArchiveOpen}
        title={t('lifecycleActions.archiveDialogTitle')}
        message={t('lifecycleActions.archiveDialogMessage', { name: studentName })}
        confirmLabel={t('lifecycleActions.archiveConfirmLabel')}
        loading={isBusy}
        error={dialogError}
        onConfirm={() => runConfirmed(onArchive, () => setIsArchiveOpen(false))}
        onCancel={() => setIsArchiveOpen(false)}
        data-testid="archive-student-dialog"
      />

      <ConfirmDialog
        open={isReactivateOpen}
        title={t('lifecycleActions.reactivateDialogTitle')}
        message={t('lifecycleActions.reactivateDialogMessage', { name: studentName })}
        confirmLabel={t('lifecycleActions.reactivateConfirmLabel')}
        confirmVariant="primary"
        loading={isBusy}
        error={dialogError}
        onConfirm={() => runConfirmed(onReactivate, () => setIsReactivateOpen(false))}
        onCancel={() => setIsReactivateOpen(false)}
        data-testid="reactivate-student-dialog"
      />
    </>
  );
}
