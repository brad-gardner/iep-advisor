import { Drawer } from '@/components/ui/drawer';
import type { SchoolStudent, UpdateSchoolStudentRequest } from '../types';
import { EditStudentForm } from './edit-student-form';

interface EditStudentDrawerProps {
  open: boolean;
  student: SchoolStudent;
  onClose: () => void;
  onSubmit: (data: UpdateSchoolStudentRequest) => Promise<{ success: boolean; error?: string }>;
}

// Long multi-section form → Drawer (keeps the student page in view). The
// Drawer unmounts its children when closed, so the form re-seeds from the
// latest `student` on every open.
export function EditStudentDrawer({ open, student, onClose, onSubmit }: EditStudentDrawerProps) {
  return (
    <Drawer open={open} onClose={onClose} title="Edit student" size="lg" data-testid="edit-student-drawer">
      <EditStudentForm student={student} onSubmit={onSubmit} onCancel={onClose} />
    </Drawer>
  );
}
