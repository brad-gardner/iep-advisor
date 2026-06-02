import { Sparkles } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { useFeatureFlag } from '@/hooks/use-feature-flags';
import { AiInterviewHelper } from '../components/ai-interview-helper';
import { WorkspaceSection } from '../components/workspace-section';
import { useStudentWorkspace } from '../hooks/use-student-workspace';
import { ENTRY_KINDS } from '../lib/entry-kinds';
import type {
  StudentWorkspaceEntryDto,
  StudentWorkspaceEntryKind,
} from '../types';

// Placeholder shown when the StudentWorkspace flag is off (matches the P7b
// welcome). Keeps the student oriented without exposing the workspace.
function WorkspaceComingSoon({ firstName }: { firstName?: string }) {
  return (
    <div className="space-y-6" data-testid="student-home">
      <div>
        <h1 className="font-serif">{firstName ? `Welcome, ${firstName}` : 'Your space'}</h1>
        <p className="mt-1 text-sm text-brand-slate-400">
          This is your space to understand and take part in your IEP.
        </p>
      </div>
      <Card className="max-w-lg" data-testid="student-home-intro">
        <div className="flex items-start gap-3">
          <Sparkles
            className="mt-0.5 h-5 w-5 shrink-0 text-brand-teal-500"
            strokeWidth={1.8}
            aria-hidden="true"
          />
          <div>
            <h2 className="mb-1 font-serif text-lg">Your workspace is coming soon</h2>
            <p className="text-sm text-brand-slate-600">
              Tools to help you understand your goals, share your voice, and prepare
              for meetings will appear here shortly. Your account is now active and
              linked to your IEP team.
            </p>
          </div>
        </div>
      </Card>
    </div>
  );
}

export function StudentHomePage() {
  const { user } = useAuth();
  const workspaceEnabled = useFeatureFlag('StudentWorkspace');
  const {
    entries,
    status,
    addEntry,
    updateEntry,
    setShareable,
    removeEntry,
    interview,
  } = useStudentWorkspace();

  if (!workspaceEnabled) {
    return <WorkspaceComingSoon firstName={user?.firstName} />;
  }

  const entriesByKind = (kind: StudentWorkspaceEntryKind): StudentWorkspaceEntryDto[] =>
    entries
      .filter((e) => e.entryKind === kind)
      .sort((a, b) => a.displayOrder - b.displayOrder);

  // AI interview answers live alongside meeting statements in the UI.
  const meetingEntries = [
    ...entriesByKind('MeetingStatement'),
    ...entriesByKind('AiInterviewAnswer'),
  ].sort((a, b) => a.displayOrder - b.displayOrder);

  const handleDelete = (id: number) => {
    if (confirm('Delete this? This cannot be undone.')) {
      void removeEntry(id);
    }
  };

  return (
    <div className="space-y-8" data-testid="student-home">
      <div>
        <h1 className="font-serif">{user?.firstName ? `Welcome, ${user.firstName}` : 'Your space'}</h1>
        <p className="mt-1 text-sm text-brand-slate-400">
          Add your strengths, interests, and what you want to say. Choose what to
          share with your team.
        </p>
      </div>

      {status === 'loading' && (
        <div className="flex justify-center py-12" data-testid="student-workspace-loading">
          <div className="h-8 w-8 animate-spin rounded-full border-b-2 border-brand-teal-500" />
        </div>
      )}

      {status === 'error' && (
        <Notice variant="error" title="We could not load your workspace">
          Please refresh the page to try again.
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
                addEntry({ entryKind: meta.kind, content, isShareable })
              }
              onUpdate={updateEntry}
              onSetShareable={setShareable}
              onDelete={handleDelete}
            />
          ))}

          <AiInterviewHelper
            onInterview={interview}
            onSave={(content, entryKind) =>
              addEntry({ entryKind, content, isShareable: false })
            }
          />
        </div>
      )}
    </div>
  );
}
