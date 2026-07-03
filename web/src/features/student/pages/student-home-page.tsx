import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { AiInterviewHelper } from '../components/ai-interview-helper';
import { WorkspaceSection } from '../components/workspace-section';
import { useStudentWorkspace } from '../hooks/use-student-workspace';
import { ENTRY_KINDS } from '../lib/entry-kinds';
import type {
  CreateWorkspaceEntryRequest,
  StudentWorkspaceEntryDto,
  StudentWorkspaceEntryKind,
} from '../types';

export function StudentHomePage() {
  const { user } = useAuth();
  const { show } = useToast();
  const {
    entries,
    status,
    reload,
    addEntry,
    updateEntry,
    setShareable,
    removeEntry,
    interview,
  } = useStudentWorkspace();

  const entriesByKind = (kind: StudentWorkspaceEntryKind): StudentWorkspaceEntryDto[] =>
    entries
      .filter((e) => e.entryKind === kind)
      .sort((a, b) => a.displayOrder - b.displayOrder);

  // AI interview answers live alongside meeting statements in the UI.
  const meetingEntries = [
    ...entriesByKind('MeetingStatement'),
    ...entriesByKind('AiInterviewAnswer'),
  ].sort((a, b) => a.displayOrder - b.displayOrder);

  // Mutations confirm success with a toast; the hook keeps local state in sync.
  const handleAdd = async (input: CreateWorkspaceEntryRequest): Promise<boolean> => {
    const ok = await addEntry(input);
    if (ok) show({ message: 'Added to your space', variant: 'success' });
    return ok;
  };

  const handleUpdate = async (
    id: number,
    content: string,
    isShareable: boolean
  ): Promise<boolean> => {
    const ok = await updateEntry(id, content, isShareable);
    if (ok) show({ message: 'Changes saved', variant: 'success' });
    return ok;
  };

  const handleDelete = (id: number) => {
    if (confirm('Delete this? This cannot be undone.')) {
      void removeEntry(id).then((ok) => {
        if (ok) show({ message: 'Entry deleted', variant: 'success' });
      });
    }
  };

  return (
    <PageLayout
      data-testid="student-home"
      title={user?.firstName ? `Welcome, ${user.firstName}` : 'Your space'}
      subtitle="Add your strengths, interests, and what you want to say — then choose what to share with your team."
    >
      {status === 'loading' && (
        <div className="flex justify-center py-12">
          <Spinner data-testid="student-workspace-loading" label="Loading your workspace…" />
        </div>
      )}

      {status === 'error' && (
        <Notice variant="error" title="We could not load your workspace">
          <p>Something went wrong. Please try again.</p>
          <div className="mt-3">
            <Button variant="secondary" size="sm" onClick={() => void reload()}>
              Try again
            </Button>
          </div>
        </Notice>
      )}

      {status === 'ready' && (
        <div className="space-y-8" data-testid="student-workspace">
          {ENTRY_KINDS.map((meta) => (
            <WorkspaceSection
              key={meta.kind}
              meta={meta}
              entries={
                meta.kind === 'MeetingStatement' ? meetingEntries : entriesByKind(meta.kind)
              }
              onAdd={(content, isShareable) =>
                handleAdd({ entryKind: meta.kind, content, isShareable })
              }
              onUpdate={handleUpdate}
              onSetShareable={setShareable}
              onDelete={handleDelete}
            />
          ))}

          <AiInterviewHelper
            onInterview={interview}
            onSave={(content, entryKind) =>
              handleAdd({ entryKind, content, isShareable: false })
            }
          />
        </div>
      )}
    </PageLayout>
  );
}
