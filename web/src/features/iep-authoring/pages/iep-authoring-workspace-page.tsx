import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { MessageSquare } from 'lucide-react';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { ActivePanel } from '../components/active-panel';
import { ChatPanel } from '../components/chat/chat-panel';
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
  const [chatOpen, setChatOpen] = useState(false);

  useFlushOnNavigate(registry);

  if (draftApi.isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading IEP draft…" />
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
      value={{
        draftId,
        studentId: Number(studentId),
        currentUserId: user.id,
        bus,
        registry,
        draftApi,
      }}
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
              <button
                type="button"
                onClick={() => setChatOpen((open) => !open)}
                aria-pressed={chatOpen}
                className="inline-flex items-center gap-1.5 rounded-button border-[1.5px] border-brand-teal-300 px-3 py-2 text-[13px] font-medium text-brand-teal-500 transition-colors hover:bg-brand-teal-50"
                data-testid="chat-toggle"
              >
                <MessageSquare className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
                {chatOpen ? 'Hide assistant' : 'Ask the assistant'}
              </button>
              <FinalizeSection
                draftId={draftId}
                studentId={Number(studentId)}
                flushBeforeFinalize={registry.flushAll}
              />
            </div>
          </div>
        </header>

        <div className={chatOpen ? 'grid grid-cols-1 gap-6 lg:grid-cols-[1fr_22rem]' : ''}>
          <div className="min-w-0 space-y-6">
            <SectionNavigator active={tab} onChange={setTab} />
            <ActivePanel tab={tab} />
          </div>
          {chatOpen && (
            <div className="lg:sticky lg:top-6 lg:h-[calc(100vh-8rem)]">
              <ChatPanel draftId={draftId} onClose={() => setChatOpen(false)} />
            </div>
          )}
        </div>
      </div>
    </IepEditorContext.Provider>
  );
}
