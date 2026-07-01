import { useEffect, useState } from 'react';
import { MessageSquareQuote } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { getChildShareableEntries } from '@/features/student/api/shareable-entries-api';
import { entryKindLabel } from '@/features/student/lib/entry-kinds';
import type { StudentWorkspaceEntryDto } from '@/features/student/types';

interface StudentSharedEntriesProps {
  childId: number;
}

/**
 * Read-only "From your student" panel for parents in Meeting Prep. Lists the
 * entries the student chose to share. Renders nothing when there are no shared
 * entries (e.g. the child has no linked student account).
 */
export function StudentSharedEntries({ childId }: StudentSharedEntriesProps) {
  const [entries, setEntries] = useState<StudentWorkspaceEntryDto[]>([]);

  useEffect(() => {
    if (!childId) return;
    let active = true;
    getChildShareableEntries(childId)
      .then((res) => {
        if (active && res.success && res.data) setEntries(res.data);
      })
      .catch(() => {
        // Non-critical surface — stay silent on failure.
      });
    return () => {
      active = false;
    };
  }, [childId]);

  if (entries.length === 0) return null;

  return (
    <Card className="space-y-3" data-testid="student-shared-entries">
      <div className="flex items-start gap-2">
        <MessageSquareQuote
          className="mt-0.5 h-5 w-5 shrink-0 text-brand-teal-500"
          strokeWidth={1.8}
          aria-hidden="true"
        />
        <div>
          <h2 className="font-serif text-lg">From your student</h2>
          <p className="text-sm text-brand-slate-400">
            What your student chose to share to help you prepare.
          </p>
        </div>
      </div>
      <ul className="space-y-2">
        {entries.map((entry) => (
          <li
            key={entry.id}
            className="rounded-card border-[0.5px] border-brand-slate-200 p-3"
            data-testid={`student-shared-entry-${entry.id}`}
          >
            <span className="block text-[11px] font-medium uppercase tracking-wide text-brand-teal-600">
              {entryKindLabel(entry.entryKind)}
            </span>
            <span className="mt-0.5 block whitespace-pre-wrap text-sm text-brand-slate-800">
              {entry.content}
            </span>
          </li>
        ))}
      </ul>
    </Card>
  );
}
