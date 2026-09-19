import { useCallback, useState } from 'react';
import { useLocation, useNavigate, useOutletContext, useSearchParams } from 'react-router-dom';
import { MessagesSquare } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Drawer } from '@/components/ui/drawer';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';
import { usePageTitle } from '@/hooks/use-page-title';
import { useAuth } from '@/features/auth/hooks/use-auth';
import type { ChildOutletContext } from '@/features/children/components/child-detail-page';
import { ADD_QUESTION_PARAM } from '@/features/meeting-prep/components/child-meeting-prep-tab';
import { JournalEntryDrawer, type JournalEntryDraft } from '@/features/journal/components/journal-entry-drawer';
import { todayInputValue } from '@/features/journal/lib/today';
import { useAdvocateThread, type SendFailure } from '../hooks/use-advocate-thread';
import { useAdvocateThreads } from '../hooks/use-advocate-threads';
import { useAdvocateUsage } from '../hooks/use-advocate-usage';
import { useAdvocateChildContext } from '../hooks/use-advocate-child-context';
import { useHasJournalEntries } from '../hooks/use-has-journal-entries';
import { parseAbout, readAboutLabel } from '../lib/about';
import { PREP_QUESTION_COPIED_TOAST, STOPPED_COPY, VIEWER_NOTICE_COPY } from '../lib/copy';
import { isUsageCapped } from '../lib/usage';
import { ADVOCATE_TITLE_MAX_LENGTH } from '../types/advocate';
import { AboutContextPill } from './about-context-pill';
import type { SuggestionHandlers } from './assistant-message';
import { Composer } from './composer';
import { AdvocateEmptyState } from './empty-state';
import { MessageList } from './message-list';
import { PrivacyBanner } from './privacy-banner';
import { StateHint } from './state-hint';
import { ThreadList } from './thread-list';
import { UsageNotice } from './usage-notice';

const THREAD_PARAM = 'thread';
const ABOUT_PARAM = 'about';

/** A thread started from the composer takes its title from the first question. */
function titleFromQuestion(text: string): string {
  const oneLine = text.replace(/\s+/g, ' ').trim();
  const max = Math.min(60, ADVOCATE_TITLE_MAX_LENGTH);
  return oneLine.length <= max ? oneLine : `${oneLine.slice(0, max - 1).trimEnd()}…`;
}

function parseThreadParam(value: string | null): number | null {
  if (!value || !/^\d+$/.test(value)) return null;
  const n = Number(value);
  return n > 0 ? n : null;
}

/**
 * `/children/:childId/advocate` — the parent's private conversations with the
 * Virtual Advocate about this child. The open thread lives in `?thread=`. A
 * launcher arrives with `?about=` and no thread: the page shows a context
 * pill, the first question starts a fresh thread, `about` is sent with that
 * message only and then removed from the URL (its raw value is never
 * displayed). The param is read at send time, so a second launcher landing on
 * the already-mounted page starts another conversation.
 */
export function AdvocatePage() {
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const canAsk = child.role === 'owner' || child.role === 'collaborator';
  usePageTitle('Advocate');
  const { show } = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();

  const [searchParams, setSearchParams] = useSearchParams();
  const selectedId = parseThreadParam(searchParams.get(THREAD_PARAM));
  // Only a value in the server grammar counts; anything else is dropped silently.
  const about = selectedId == null ? parseAbout(searchParams.get(ABOUT_PARAM)) : null;
  const aboutLabel = readAboutLabel(location.state);

  const [draft, setDraft] = useState('');
  const [railOpen, setRailOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [journalDraft, setJournalDraft] = useState<JournalEntryDraft | null>(null);

  const threadsApi = useAdvocateThreads(childId);
  const usageApi = useAdvocateUsage();
  const resolvedState = useAdvocateChildContext(childId);
  const hasJournalEntries = useHasJournalEntries(childId);

  // One navigation per event: react-router's functional `setSearchParams`
  // reads the params captured at render, so two back-to-back updates would
  // overwrite each other.
  const updateParams = useCallback(
    (changes: { thread?: number | null; dropAbout?: boolean }) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          if (changes.thread !== undefined) {
            if (changes.thread == null) next.delete(THREAD_PARAM);
            else next.set(THREAD_PARAM, String(changes.thread));
          }
          if (changes.dropAbout) next.delete(ABOUT_PARAM);
          return next;
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  // Opening a thread (or a blank page) leaves any launcher context behind.
  const select = useCallback(
    (id: number | null) => {
      updateParams({ thread: id, dropAbout: true });
      setRailOpen(false);
    },
    [updateParams],
  );

  const handleFailure = useCallback(
    (failure: SendFailure) => {
      if (failure.code === 'usage_cap') usageApi.markCapped();
      if (failure.code === 'not_found') {
        threadsApi.reload();
        select(null);
      }
    },
    [usageApi, threadsApi, select],
  );

  const thread = useAdvocateThread(selectedId, {
    onAnswered: (id) => {
      usageApi.reload();
      threadsApi.touch(id);
    },
    onFailure: handleFailure,
  });

  const capped = isUsageCapped(usageApi.usage);
  const busy = thread.isStreaming || creating;

  const handleSend = async (text: string) => {
    if (!canAsk || capped || busy) return;
    let target = selectedId;
    let created = false;
    if (target == null) {
      setCreating(true);
      setCreateError(null);
      try {
        target = (await threadsApi.create(titleFromQuestion(text))).id;
        created = true;
      } catch (err) {
        setCreateError(err instanceof Error ? err.message : 'Could not start a conversation.');
        return;
      } finally {
        setCreating(false);
      }
    }
    // The launcher context rides along with the first message only.
    const aboutValue = created && about ? `${about.kind}:${about.id}` : undefined;
    if (created) updateParams({ thread: target, dropAbout: true });
    setRailOpen(false);
    if (thread.send(text, { threadId: target, about: aboutValue })) setDraft('');
  };

  const handleNew = () => {
    select(null);
    setDraft('');
    setCreateError(null);
  };

  const handleDelete = async (id: number) => {
    await threadsApi.remove(id);
    if (id === selectedId) select(null);
  };

  const suggestionHandlers: SuggestionHandlers = {
    onAddPrepQuestion: (text) => {
      const params = new URLSearchParams({ [ADD_QUESTION_PARAM]: text });
      navigate(`/children/${childId}/meeting-prep?${params.toString()}`);
    },
    onCopyPrepQuestion: (text) => {
      const clipboard = typeof navigator !== 'undefined' ? navigator.clipboard : undefined;
      if (!clipboard?.writeText) {
        show({ message: 'Couldn’t copy — select the question and copy it yourself.', variant: 'error' });
        return;
      }
      clipboard
        .writeText(text)
        .then(() => show({ message: PREP_QUESTION_COPIED_TOAST, variant: 'success' }))
        .catch(() => show({ message: 'Couldn’t copy — select the question and copy it yourself.', variant: 'error' }));
    },
    onJournalEntry: (text, date) => {
      const today = todayInputValue();
      const validDate = date && /^\d{4}-\d{2}-\d{2}$/.test(date) && date <= today ? date : undefined;
      setJournalDraft({ contentMarkdown: text, occurredOn: validDate });
    },
  };

  const hasLocalActivity = thread.pending !== null || thread.streaming !== null;
  // A just-created thread is still loading (empty) while its first message streams — never hide that behind a spinner.
  const showLoading = thread.loading && !hasLocalActivity;
  const conversationEmpty = !thread.loading && thread.messages.length === 0 && !hasLocalActivity;
  const threadCount = threadsApi.threads?.length ?? 0;

  const rail = (
    <ThreadList
      threads={threadsApi.threads}
      error={threadsApi.error}
      selectedId={selectedId}
      onSelect={select}
      onNew={handleNew}
      onRename={threadsApi.rename}
      onDelete={handleDelete}
      canAsk={canAsk}
      busy={busy}
    />
  );

  return (
    <div className="space-y-3" data-testid="advocate-page">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-serif">Ask the advocate about {child.firstName}</h2>
          <p className="mt-1 text-sm text-brand-slate-400">Plain answers about the plan, your rights, and what to do next.</p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          className="md:hidden"
          onClick={() => setRailOpen(true)}
          data-testid="advocate-open-rail"
        >
          <MessagesSquare className="mr-1 h-4 w-4" aria-hidden="true" />
          Conversations{threadCount > 0 ? ` (${threadCount})` : ''}
        </Button>
      </div>

      <PrivacyBanner />
      <UsageNotice usage={usageApi.usage} />
      {!canAsk && (
        <Notice variant="info" title={VIEWER_NOTICE_COPY} data-testid="advocate-viewer-notice">
          Only the parents who manage {child.firstName}'s profile can start a conversation.
        </Notice>
      )}

      <div className="gap-4 md:grid md:grid-cols-[15rem_minmax(0,1fr)]">
        <aside className="hidden md:block" aria-label="Conversations">
          {rail}
        </aside>

        <section className="space-y-3" aria-label="Conversation" data-testid="advocate-conversation">
          {showLoading && (
            <div className="flex justify-center py-12">
              <Spinner label="Loading conversation…" />
            </div>
          )}

          {thread.loadError && (
            <div role="alert">
              <Notice variant="error" title={thread.loadError}>
                <Button variant="secondary" size="sm" className="mt-2" onClick={thread.reload}>
                  Try again
                </Button>
              </Notice>
            </div>
          )}

          {conversationEmpty && !thread.loadError && (
            <AdvocateEmptyState
              childFirstName={child.firstName}
              canAsk={canAsk && !capped}
              onPickExample={setDraft}
              hasJournalEntries={hasJournalEntries === true}
            />
          )}

          {!conversationEmpty && !showLoading && (
            <MessageList
              childId={childId}
              messages={thread.messages}
              pending={thread.pending}
              streaming={thread.streaming}
              handlers={suggestionHandlers}
            />
          )}

          {thread.stopped && (
            <p className="text-xs text-brand-slate-500" role="status" data-testid="advocate-stopped">
              {STOPPED_COPY}
            </p>
          )}

          {thread.failure && (
            <div role="alert" data-testid="advocate-send-error">
              <Notice variant="error" title={thread.failure.message}>
                {thread.failure.retryable && (
                  <Button variant="secondary" size="sm" className="mt-2" onClick={thread.retry} data-testid="advocate-retry">
                    Retry
                  </Button>
                )}
              </Notice>
            </div>
          )}

          {createError && (
            <div role="alert">
              <Notice variant="error" title={createError} />
            </div>
          )}

          {canAsk && (
            <div
              className="sticky bottom-0 -mx-4 space-y-2 border-t border-brand-slate-100 bg-white px-4 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] sm:-mx-6 sm:px-6 md:static md:mx-0 md:border-0 md:px-0 md:pb-0"
              data-testid="advocate-composer-dock"
            >
              {about && (
                <AboutContextPill about={about} label={aboutLabel} onClear={() => updateParams({ dropAbout: true })} />
              )}
              {user && resolvedState === null && <StateHint userId={user.id} />}
              <Composer
                value={draft}
                onChange={setDraft}
                onSend={(text) => void handleSend(text)}
                onStop={thread.stop}
                streaming={thread.isStreaming}
                disabled={capped || creating}
                disabledReason={capped ? 'You’ve used this year’s advocate messages.' : undefined}
                childFirstName={child.firstName}
              />
            </div>
          )}

          {thread.disclaimer && thread.messages.length > 0 && (
            <p className="text-[11px] leading-relaxed text-brand-slate-400" data-testid="advocate-disclaimer">
              {thread.disclaimer}
            </p>
          )}
        </section>
      </div>

      <Drawer open={railOpen} onClose={() => setRailOpen(false)} title="Conversations" data-testid="advocate-rail-drawer">
        {rail}
      </Drawer>

      {canAsk && (
        <JournalEntryDrawer
          open={journalDraft !== null}
          onClose={() => setJournalDraft(null)}
          childId={childId}
          initial={journalDraft ?? undefined}
          onSaved={() => {
            setJournalDraft(null);
            show({ message: 'Update added to the journal', variant: 'success' });
          }}
          data-testid="advocate-journal-drawer"
        />
      )}
    </div>
  );
}

export default AdvocatePage;
