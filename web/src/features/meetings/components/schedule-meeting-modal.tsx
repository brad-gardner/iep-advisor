import { useState } from 'react';
import { Drawer } from '@/components/ui/drawer';
import { ScheduleMeetingForm } from './schedule-meeting-form';
import type { MeetingDto } from '../types';

interface ScheduleMeetingModalProps {
  open: boolean;
  onClose: () => void;
  studentId: number;
  studentName?: string;
  /** Present for reschedule/edit; absent for a fresh "schedule meeting". */
  meeting?: MeetingDto;
  onSaved: (meeting: MeetingDto) => void;
}

// A long multi-section form (details + participant checklist) → Drawer, per
// the design system's Modal/Drawer split — named "Modal" here to match the
// deliverable's plan-4 naming. The Drawer unmounts its children while closed,
// so the form re-seeds fresh every time it's opened.
export function ScheduleMeetingModal({
  open,
  onClose,
  studentId,
  studentName,
  meeting,
  onSaved,
}: ScheduleMeetingModalProps) {
  const title = meeting
    ? `Reschedule meeting${studentName ? ` — ${studentName}` : ''}`
    : `Schedule meeting${studentName ? ` — ${studentName}` : ''}`;

  // A save that is in flight cannot be dismissed away — it would still land and fire onSaved.
  const [submitting, setSubmitting] = useState(false);
  return (
    <Drawer open={open} onClose={onClose} preventClose={submitting} title={title} size="lg" data-testid="schedule-meeting-modal">
      <ScheduleMeetingForm
        studentId={studentId}
        meeting={meeting}
        onSaved={(saved) => {
          onSaved(saved);
          onClose();
        }}
        onCancel={onClose}
        onSubmittingChange={setSubmitting}
      />
    </Drawer>
  );
}
