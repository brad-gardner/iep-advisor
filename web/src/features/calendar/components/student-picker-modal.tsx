import { useEffect, useState } from 'react';
import { Modal } from '@/components/ui/modal';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import { searchStudents } from '@/features/educator/api/educator-api';
import type { SchoolStudent } from '@/features/educator/types';

interface StudentPickerModalProps {
  open: boolean;
  onClose: () => void;
  onSelect: (student: SchoolStudent) => void;
}

/** Search-and-pick a student, used to start "Schedule meeting" from the
 * calendar (which has no student in context yet). */
export function StudentPickerModal({ open, onClose, onSelect }: StudentPickerModalProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SchoolStudent[] | null>(null);

  useEffect(() => {
    if (!open) return;
    let active = true;
    const timer = setTimeout(async () => {
      try {
        const response = await searchStudents({ query: query || undefined, pageSize: 25 });
        if (active && response.success && response.data) setResults(response.data.items);
      } catch {
        if (active) setResults([]);
      }
    }, 250);
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [open, query]);

  return (
    <Modal open={open} onClose={onClose} title="Schedule meeting" data-testid="student-picker-modal">
      <div className="space-y-3">
        <Input
          id="student-picker-search"
          label="Find a student"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Name or student ID"
          autoFocus
        />
        {results === null ? (
          <div className="flex justify-center py-6">
            <Spinner label="Searching…" />
          </div>
        ) : results.length === 0 ? (
          <p className="py-4 text-center text-sm text-brand-slate-500">No students found.</p>
        ) : (
          <ul className="max-h-72 divide-y divide-brand-slate-100 overflow-y-auto rounded-input border border-brand-slate-200">
            {results.map((student) => (
              <li key={student.id}>
                <button
                  type="button"
                  onClick={() => onSelect(student)}
                  className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-brand-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-brand-teal-400"
                  data-testid={`student-picker-option-${student.id}`}
                >
                  <span>
                    {student.firstName} {student.lastName}
                  </span>
                  <span className="text-xs text-brand-slate-500">{student.externalStudentId}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
