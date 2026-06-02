import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Notice } from '@/components/ui/notice';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { ActivePanel } from '../components/active-panel';
import { SaveStatusIndicator } from '../components/save-status-indicator';
import { SectionNavigator } from '../components/section-navigator';
import type { WorkspaceTab } from '../lib/workspace-tabs';
import { IepEditorContext } from '../hooks/use-editor-context';
import { useFlushOnNavigate } from '../hooks/use-flush-on-navigate';
import { useFlushRegistry } from '../hooks/use-flush-registry';
import { useIepDraft } from '../hooks/use-iep-draft';
import { useSaveStatusBus } from '../hooks/use-save-status-bus';
import { FinalizeSection } from '@/features/iep-versions/components/finalize-section';

export function IepAuthoringWorkspacePage() {
  const { studentId, draftId: draftIdParam } = useParams<{
    studentId: string;
    draftId: string;
  }>();
  const draftId = Number(draftIdParam);
  const { user } = useAuth();

  const draftApi = useIepDraft(draftId);
  const bus = useSaveStatusBus();
  const registry = useFlushRegistry();
  const [tab, setTab] = useState<WorkspaceTab>('goals');

  useFlushOnNavigate(registry);

  if (draftApi.isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (draftApi.error || !draftApi.draft || !user) {
    return (
      <div className="space-y-4">
        <Notice variant="error" title="Could not load this IEP draft">
          {draftApi.error ?? 'The draft is unavailable.'}
        </Notice>
        <Link
          to={`/educator/students/${studentId}/iep-drafts`}
          className="text-sm text-brand-teal-500 hover:underline"
        >
          ← Back to IEP drafts
        </Link>
      </div>
    );
  }

  return (
    <IepEditorContext.Provider
      value={{ draftId, currentUserId: user.id, bus, registry, draftApi }}
    >
      <div className="space-y-6">
        <header className="space-y-2">
          <Link
            to={`/educator/students/${studentId}/iep-drafts`}
            className="text-sm text-brand-teal-500 hover:underline"
          >
            ← Back to IEP drafts
          </Link>
          <div className="flex items-center justify-between gap-4">
            <h1 className="font-serif">{draftApi.draft.title || 'Untitled IEP draft'}</h1>
            <div className="flex items-center gap-4">
              <SaveStatusIndicator status={bus.status} />
              <FinalizeSection
                draftId={draftId}
                studentId={Number(studentId)}
                flushBeforeFinalize={registry.flushAll}
              />
            </div>
          </div>
        </header>

        <SectionNavigator active={tab} onChange={setTab} />
        <ActivePanel tab={tab} />
      </div>
    </IepEditorContext.Provider>
  );
}
