import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import type { ChildProfile, User } from '@/types/api';
import type { StreamAdvocateMessageHandlers } from '../api/advocate-api';
import type { AdvocateMessageDto, AdvocateThreadDto, SendAdvocateMessageRequest } from '../types/advocate';
import { EXAMPLE_QUESTIONS, JOURNAL_EXAMPLE_QUESTION, PREP_QUESTION_COPIED_TOAST, TRUNCATED_NOTICE_COPY } from '../lib/copy';
import { STATE_HINT_COPY } from './state-hint';

interface FakeStream {
  threadId: number;
  body: SendAdvocateMessageRequest;
  handlers: StreamAdvocateMessageHandlers;
  resolve: () => void;
  reject: (err: unknown) => void;
}

const api = vi.hoisted(() => ({
  streams: [] as FakeStream[],
  listAdvocateThreads: vi.fn(),
  createAdvocateThread: vi.fn(),
  getAdvocateThread: vi.fn(),
  renameAdvocateThread: vi.fn(),
  deleteAdvocateThread: vi.fn(),
  getAdvocateUsage: vi.fn(),
  getAdvocateChildContext: vi.fn(),
  streamAdvocateMessage: vi.fn(),
}));
vi.mock('../api/advocate-api', async () => {
  const actual = await vi.importActual<typeof import('../api/advocate-api')>('../api/advocate-api');
  return { ...actual, ...api };
});

const toast = vi.hoisted(() => ({ show: vi.fn() }));
vi.mock('@/components/ui/toast', () => ({ useToast: () => toast }));
vi.mock('@/features/journal/hooks/use-journal-link-options', () => ({
  useJournalLinkOptions: () => ({ options: { ieps: [], etrs: [], meetings: [] }, loading: false }),
}));
const journalApi = vi.hoisted(() => ({
  listJournalEntries: vi.fn(),
  createJournalEntry: vi.fn(),
  updateJournalEntry: vi.fn(),
  deleteJournalEntry: vi.fn(),
}));
vi.mock('@/features/journal/api/journal-api', () => journalApi);
const auth = vi.hoisted(() => ({ user: null as User | null }));
vi.mock('@/features/auth/hooks/use-auth', () => ({ useAuth: () => ({ user: auth.user }) }));
vi.mock('@/features/subscription/components/subscribe-button', () => ({
  SubscribeButton: () => (
    <button type="button" data-testid="subscribe-button">
      Subscribe
    </button>
  ),
}));

import { AdvocateRequestError } from '../api/advocate-api';
import { AdvocatePage } from './advocate-page';

const child = (role: ChildProfile['role']): ChildProfile => ({
  id: 4,
  firstName: 'Jordan',
  lastName: 'Lee',
  dateOfBirth: null,
  gradeLevel: null,
  disabilityCategory: null,
  schoolDistrict: null,
  role,
  currentIepDocumentId: null,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
});

const thread = (id: number, title: string): AdvocateThreadDto => ({
  id,
  childProfileId: 4,
  title,
  createdAt: '2026-09-10T12:00:00Z',
  updatedAt: '2026-09-10T12:00:00Z',
  lastMessageAt: '2026-09-10T12:00:00Z',
});

const userMsg = (id: number, text: string): AdvocateMessageDto => ({
  id,
  role: 'User',
  contentMarkdown: text,
  citations: [],
  suggestions: [],
  truncated: false,
  createdAt: '2026-09-10T12:00:00Z',
});

const answer: AdvocateMessageDto = {
  id: 12,
  role: 'Assistant',
  contentMarkdown: 'Prior written notice is the **letter** the school must send.',
  citations: [
    { kind: 'kb', id: 3, label: 'Prior written notice' },
    { kind: 'child', id: 4, label: 'Child profile' },
  ],
  suggestions: [
    { kind: 'prep_question', text: 'When will I get prior written notice about this change?' },
    { kind: 'journal_entry', text: 'School said no to the evaluation on 9/10.', date: '2026-09-10' },
    { kind: 'open_kb', id: 3, text: 'Read the PWN guide' },
    { kind: 'open_goal', text: 'Look at the reading goal' },
  ],
  truncated: true,
  createdAt: '2026-09-10T12:01:00Z',
};

const detail = (id: number, messages: AdvocateMessageDto[]) => ({
  success: true,
  data: { ...thread(id, `Thread ${id}`), messages, disclaimer: 'Not legal advice.' },
});

const parent = (state: string | null): User => ({
  id: 9,
  email: 'p@example.com',
  firstName: 'Pat',
  lastName: 'Lee',
  state,
  role: 'Parent',
  fullName: 'Pat Lee',
  onboardingCompleted: true,
  subscriptionStatus: 'active',
});

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
}

/** Stands in for a launcher elsewhere in the app: navigates to the advocate page with router state. */
function Launcher({ to, label }: { to: string; label?: string }) {
  const navigate = useNavigate();
  return (
    <button type="button" onClick={() => navigate(to, label ? { state: { aboutLabel: label } } : undefined)} data-testid="fake-launcher">
      launch
    </button>
  );
}

function renderPage(role: ChildProfile['role'] = 'owner', url = '/children/4/advocate', launcher?: { to: string; label?: string }) {
  const ctx = { child: child(role), childId: 4, reloadChild: () => Promise.resolve() };
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/children/:childId" element={<Outlet context={ctx} />}>
          <Route
            path="advocate"
            element={
              <>
                <AdvocatePage />
                <LocationProbe />
                {launcher && <Launcher {...launcher} />}
              </>
            }
          />
          <Route
            path="meeting-prep"
            element={
              <>
                <output data-testid="meeting-prep-page">meeting prep</output>
                <LocationProbe />
              </>
            }
          />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

const composer = () => screen.getByTestId('advocate-composer-input') as HTMLTextAreaElement;

function typeAndSend(text: string) {
  fireEvent.change(composer(), { target: { value: text } });
  fireEvent.keyDown(composer(), { key: 'Enter' });
}

/** The most recent stream the page opened. */
const lastStream = () => api.streams[api.streams.length - 1];

describe('AdvocatePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    auth.user = parent('OH');
    journalApi.listJournalEntries.mockResolvedValue({ success: true, data: [] });
    api.streams.length = 0;
    api.streamAdvocateMessage.mockImplementation(
      (threadId: number, body: SendAdvocateMessageRequest, handlers: StreamAdvocateMessageHandlers) =>
        new Promise<void>((resolve, reject) => {
          api.streams.push({ threadId, body, handlers, resolve, reject });
          handlers.signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')));
        }),
    );
    api.listAdvocateThreads.mockResolvedValue({ success: true, data: [thread(1, 'PWN question'), thread(2, 'ETR timing')] });
    api.getAdvocateThread.mockImplementation((id: number) =>
      Promise.resolve(id === 1 ? detail(1, [userMsg(11, 'What is PWN?'), answer]) : detail(id, [])),
    );
    api.getAdvocateUsage.mockResolvedValue({ success: true, data: { used: 10, limit: 300, subscriptionActive: true } });
    api.getAdvocateChildContext.mockResolvedValue({ success: true, data: { stateCode: 'OH' } });
    api.createAdvocateThread.mockImplementation((_childId: number, title?: string) =>
      Promise.resolve({ success: true, data: thread(3, title ?? 'New conversation') }),
    );
    api.renameAdvocateThread.mockResolvedValue({ success: true });
    api.deleteAdvocateThread.mockResolvedValue({ success: true });
  });

  it('opens on an empty conversation whose example questions prefill the composer without sending', async () => {
    renderPage();
    await screen.findByTestId('advocate-empty');
    expect(screen.getByTestId('advocate-privacy-banner')).toHaveTextContent('Private — only you can see this.');
    const examples = screen.getAllByTestId('advocate-example');
    expect(examples.map((b) => b.textContent)).toEqual(EXAMPLE_QUESTIONS);
    fireEvent.click(examples[0]);
    expect(composer().value).toBe('What is prior written notice?');
    expect(api.streamAdvocateMessage).not.toHaveBeenCalled();
    expect(screen.queryByTestId('advocate-usage-warning')).not.toBeInTheDocument();
  });

  it('does not repeat the section heading inside the empty state', async () => {
    renderPage();
    await screen.findByTestId('advocate-empty');
    // The section heading above the panel is the only place this says it.
    expect(screen.getByRole('heading', { level: 2, name: 'Ask the advocate about Jordan' })).toBeInTheDocument();
    expect(screen.queryByText("Ask about Jordan's plan, your rights, or what to do next")).not.toBeInTheDocument();
  });

  it('nests the composer inside one conversation panel with a single scroller once a thread has messages', async () => {
    renderPage('owner', '/children/4/advocate?thread=1');
    await screen.findByTestId('advocate-assistant-message');

    // The composer dock is a descendant of the panel, not a separate floating band.
    const panel = screen.getByTestId('advocate-conversation');
    expect(within(panel).getByTestId('advocate-composer-dock')).toBeInTheDocument();

    // Exactly one scroller in the conversation area — MessageList's own region
    // owns the scroll; the panel does not add a second wrapping scroller.
    const scrollers = within(panel).getAllByRole('region', { name: 'Conversation' });
    expect(scrollers).toHaveLength(1);
    expect(scrollers[0]).toBe(screen.getByTestId('advocate-messages'));
    // ...and structurally: a re-added `overflow-y-auto` wrapper around MessageList would not be a
    // named region, so the role query above cannot catch it on its own.
    const overflowing = panel.querySelectorAll('[class*="overflow-y-auto"]');
    expect(overflowing).toHaveLength(1);
    expect(overflowing[0]).toBe(scrollers[0]);

    // Guards the class, which is as far as jsdom can go — it has no layout, so the clipping itself
    // cannot be measured here. The sr-only speaker labels inside the bubbles are `position:
    // absolute`, and an absolutely-positioned box is only clipped by an ancestor's `overflow` when
    // that ancestor is also its containing block. Dropping `relative` lets them lay out against the
    // initial containing block instead: measured at 400x820 with four messages they landed ~857px
    // below the content and pushed `documentElement.scrollHeight` from 1250 to 2107, which is both
    // dead phone scroll and a broken page-bottom test for the pin guard.
    expect(scrollers[0]).toHaveClass('relative');
  });

  it('publishes the summed height of everything the sticky dock paints over', async () => {
    // jsdom has no ResizeObserver and no layout, so the measurement is driven by hand: the stub
    // keeps its callback so the test can re-fire it after giving the two observed boxes real
    // heights, which is the only way to assert the arithmetic rather than just `0px`.
    // The effect re-creates the observer when the dock or the notice row mounts, so only the live
    // instance's targets are interesting — an accumulating list would also hold the torn-down one's.
    let observed: Element[] = [];
    let fire = () => {};
    vi.stubGlobal(
      'ResizeObserver',
      class {
        callback: () => void;
        constructor(callback: () => void) {
          this.callback = callback;
          observed = [];
          fire = callback;
        }
        observe(target: Element) {
          observed.push(target);
          this.callback();
        }
        disconnect() {}
      },
    );

    try {
      renderPage('owner', '/children/4/advocate?thread=1');
      await screen.findByTestId('advocate-assistant-message');

      const panel = screen.getByTestId('advocate-conversation');
      const dock = screen.getByTestId('advocate-composer-dock');
      // Both boxes the sticky dock paints over are measured, not just the composer: the notice row
      // (stopped / Retry / disclaimer) sits between the scroller and the dock and is covered too.
      expect(observed).toContain(dock);
      expect(observed).toHaveLength(2);
      const notices = observed.find((el) => el !== dock) as HTMLElement;
      expect(notices).toContainElement(screen.getByTestId('advocate-disclaimer'));

      // Real dock heights measured in-browser at 400x820: 152px with the composer and the
      // disclaimer row's 89px above it.
      Object.defineProperty(dock, 'offsetHeight', { value: 152, configurable: true });
      Object.defineProperty(notices, 'offsetHeight', { value: 89, configurable: true });
      act(() => fire());
      expect(panel.style.getPropertyValue('--advocate-dock-h')).toBe('241px');

      // A later resize (the About pill mounting, the composer growing) re-publishes through the
      // same observer rather than being measured once at mount.
      Object.defineProperty(dock, 'offsetHeight', { value: 261, configurable: true });
      act(() => fire());
      expect(panel.style.getPropertyValue('--advocate-dock-h')).toBe('350px');

      const sentinel = screen.getByTestId('advocate-scroll-tail');
      expect(sentinel.className).toContain('scroll-mb-[var(--advocate-dock-h,10rem)]');
      expect(sentinel.className).toContain('md:scroll-mb-0');
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it('follows new content to the bottom, and stops following once the reader scrolls the conversation up', async () => {
    const targets: Element[] = [];
    const spy = vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(function (this: Element) {
      targets.push(this);
    });

    try {
      renderPage('owner', '/children/4/advocate?thread=1');
      await screen.findByTestId('advocate-assistant-message');
      const tail = screen.getByTestId('advocate-scroll-tail');
      const scroller = screen.getByTestId('advocate-messages');

      targets.length = 0;
      typeAndSend('And what if they refuse?');
      await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
      // The reader asked for this one, so it pins regardless of where they were.
      expect(targets).toContain(tail);

      // Reader scrolls back up to re-read something: 1500px from the bottom, well past the 48px
      // threshold. jsdom reports 0 for every scroll metric, so they are supplied here.
      Object.defineProperty(scroller, 'scrollHeight', { value: 2000, configurable: true });
      Object.defineProperty(scroller, 'clientHeight', { value: 500, configurable: true });
      scroller.scrollTop = 0;
      fireEvent.scroll(scroller);

      targets.length = 0;
      act(() => lastStream().handlers.onDelta('They have to tell you '));
      act(() => lastStream().handlers.onDelta('in writing.'));
      expect(screen.getByTestId('advocate-streaming-text')).toHaveTextContent('They have to tell you in writing.');
      expect(targets).not.toContain(tail);
    } finally {
      spy.mockRestore();
    }
  });

  it('does not drag the page back down on every streamed token when the reader has scrolled the page away', async () => {
    // `scrollIntoView` satisfies the sentinel's reserve against every scrollable ancestor, the
    // document included — and below `md` the page is the scroller. The conversation region does not
    // move when the page does, so its own scroll handler never fires: without a page-level pin the
    // reader is dragged back down once per token (measured in-browser: 386 -> 0 -> 386).
    const targets: Element[] = [];
    const spy = vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(function (this: Element) {
      targets.push(this);
    });
    const innerHeight = Object.getOwnPropertyDescriptor(window, 'innerHeight');
    const scrollY = Object.getOwnPropertyDescriptor(window, 'scrollY');

    const setPage = (y: number, event: 'scroll' | 'resize' = 'scroll') => {
      Object.defineProperty(window, 'scrollY', { value: y, configurable: true });
      fireEvent(window, new Event(event));
    };

    try {
      Object.defineProperty(window, 'innerHeight', { value: 820, configurable: true });
      Object.defineProperty(document.documentElement, 'scrollHeight', { value: 1250, configurable: true });

      renderPage('owner', '/children/4/advocate?thread=1');
      await screen.findByTestId('advocate-assistant-message');
      const tail = screen.getByTestId('advocate-scroll-tail');

      typeAndSend('And what if they refuse?');
      await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));

      // Reader scrolls the page to the top while the answer streams.
      setPage(0);
      targets.length = 0;
      act(() => lastStream().handlers.onDelta('They have to tell you '));
      expect(targets).not.toContain(tail);

      // The answer landing must not re-pin the page either: settling changes `messages.length`,
      // and folding the page into the "a new message always pins" rule would move the whole page
      // at the one moment the reader is least expecting it.
      api.getAdvocateThread.mockImplementation((id: number) =>
        Promise.resolve(id === 1 ? detail(1, [userMsg(11, 'What is PWN?'), answer, { ...answer, id: 33 }]) : detail(id, [])),
      );
      targets.length = 0;
      await act(async () => {
        lastStream().handlers.onDone({
          messageId: 33,
          contentMarkdown: answer.contentMarkdown,
          citations: [],
          suggestions: [],
          truncated: false,
          disclaimer: 'Not legal advice.',
        });
        lastStream().resolve();
        await Promise.resolve();
      });
      expect(targets).not.toContain(tail);

      // ...but it is offered rather than silently withheld: the sr-only status node announces the
      // answer to AT, and this is the sighted equivalent.
      const jump = await screen.findByTestId('advocate-jump-latest');
      targets.length = 0;
      fireEvent.click(jump);
      expect(targets).toContain(tail);
      expect(screen.queryByTestId('advocate-jump-latest')).not.toBeInTheDocument();
      // Taking the offer must not drop focus to <body>: the button unmounts itself on click.
      expect(screen.getByTestId('advocate-messages')).toHaveFocus();

      // Sending re-pins the page on its own, from wherever the reader happens to be — not because
      // they scrolled back first. (386 would be within PAGE_PIN_THRESHOLD_PX of the 430 maximum and
      // so would pin through the ordinary scroll path, proving nothing about the send rule.)
      setPage(0);
      targets.length = 0;
      typeAndSend('And in writing?');
      await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(2));
      expect(targets).toContain(tail);

      // Scroll away again, let the second answer land off-screen, and this time arrive at the
      // bottom without taking the offer: it has to clear itself. `resize` as well as `scroll`,
      // because a rotation or the keyboard can put the reader at the bottom without either.
      setPage(0);
      api.getAdvocateThread.mockImplementation((id: number) =>
        Promise.resolve(
          id === 1 ? detail(1, [userMsg(11, 'What is PWN?'), answer, { ...answer, id: 33 }, { ...answer, id: 44 }]) : detail(id, []),
        ),
      );
      await act(async () => {
        lastStream().handlers.onDone({
          messageId: 44,
          contentMarkdown: answer.contentMarkdown,
          citations: [],
          suggestions: [],
          truncated: false,
          disclaimer: 'Not legal advice.',
        });
        lastStream().resolve();
        await Promise.resolve();
      });
      await screen.findByTestId('advocate-jump-latest');
      setPage(386, 'resize');
      expect(screen.queryByTestId('advocate-jump-latest')).not.toBeInTheDocument();
    } finally {
      spy.mockRestore();
      if (innerHeight) Object.defineProperty(window, 'innerHeight', innerHeight);
      if (scrollY) Object.defineProperty(window, 'scrollY', scrollY);
      delete (document.documentElement as unknown as Record<string, unknown>).scrollHeight;
    }
  });

  it('tints the whole composer while input is paused and reddens its border over the limit', async () => {
    // Regression guard for a fix that silently removed both affordances: `cn` is a plain join, so a
    // conditional class layered over a base one is decided by CSS source order (`bg-white` is
    // emitted after `bg-brand-slate-50`, `border-brand-slate-200` after `border-brand-danger-200`)
    // and never painted. Each pair has to emit exactly one utility.
    renderPage('owner', '/children/4/advocate?thread=2');
    await screen.findByTestId('advocate-empty');
    const field = () => composer().parentElement as HTMLElement;

    expect(field()).toHaveClass('bg-white');
    expect(field()).not.toHaveClass('bg-brand-slate-50');

    fireEvent.change(composer(), { target: { value: 'x'.repeat(2001) } });
    expect(field()).toHaveClass('border-brand-danger-200');
    expect(field()).not.toHaveClass('border-brand-slate-200');

    fireEvent.change(composer(), { target: { value: 'What is PWN?' } });
    fireEvent.keyDown(composer(), { key: 'Enter' });
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));

    // Streaming: the textarea is read-only (never disabled — that would blur it), and the tint is on
    // the wrapper so the counter row greys out with it rather than staying white underneath.
    expect(composer()).toHaveAttribute('readonly');
    expect(field()).toHaveClass('bg-brand-slate-50');
    expect(field()).not.toHaveClass('bg-white');
    expect(field()).toHaveClass('border-brand-slate-200');
  });

  it('lets thread titles wrap onto two lines instead of truncating', async () => {
    const longTitle =
      'A conversation title long enough that the old single-line truncate-with-ellipsis behavior would have cut it short';
    api.listAdvocateThreads.mockResolvedValue({ success: true, data: [thread(1, longTitle)] });
    renderPage();
    const rail = await screen.findByTestId('advocate-thread-list');
    // Rendered in full (not sliced with an ellipsis) — CSS handles any two-line wrapping, not JS truncation.
    const title = within(rail).getByText(longTitle);
    expect(title.className).not.toMatch(/\btruncate\b/);
    // Two lines, not unbounded: an 8-line title in a 16rem rail would push the rest of the list off.
    expect(title).toHaveClass('line-clamp-2');
  });

  it('sends from a blank page: creates a thread, shows the user bubble at once, streams tools and deltas, then renders the stored answer with sources and suggestions', async () => {
    renderPage('owner', '/children/4/advocate?about=iep:12');
    await screen.findByTestId('advocate-empty');

    typeAndSend('What is prior written notice?');

    await waitFor(() => expect(api.createAdvocateThread).toHaveBeenCalledWith(4, 'What is prior written notice?'));
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
    const stream = lastStream();
    expect(stream.threadId).toBe(3);
    expect(stream.body).toEqual({ text: 'What is prior written notice?', about: 'iep:12' });
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/children/4/advocate?thread=3'));
    expect(screen.getByTestId('advocate-user-message-pending')).toHaveTextContent('What is prior written notice?');
    expect(composer().value).toBe('');
    expect(composer()).toHaveAttribute('readonly');
    expect(screen.getByTestId('advocate-stop')).toBeInTheDocument();

    act(() => stream.handlers.onTool({ name: 'search_knowledge_base', label: 'Checking the rules', status: 'started' }));
    expect(screen.getByTestId('advocate-tool-started')).toHaveTextContent('Checking the rules…');
    act(() => stream.handlers.onTool({ name: 'search_knowledge_base', label: 'Checking the rules', status: 'finished' }));
    expect(screen.queryByTestId('advocate-tool-started')).not.toBeInTheDocument();
    expect(screen.getByTestId('advocate-tool-finished')).toHaveTextContent('Checking the rules');

    act(() => stream.handlers.onDelta('Prior written '));
    act(() => stream.handlers.onDelta('notice is'));
    expect(screen.getByTestId('advocate-streaming-text')).toHaveTextContent('Prior written notice is');
    // The streaming bubble is hidden from assistive tech (not a live region) so
    // tokens are never re-read; the text bubble is hidden from AT while tool rows stay announced.
    expect(screen.getByTestId('advocate-streaming-text').closest('[aria-hidden="true"]')).not.toBeNull();
    expect(screen.getByTestId('advocate-messages')).not.toHaveAttribute('aria-busy');
    // No answer has finished yet — the sr-only status node has nothing to announce.
    expect(screen.getByTestId('advocate-announcement')).toBeEmptyDOMElement();

    api.getAdvocateThread.mockImplementation((id: number) =>
      Promise.resolve(
        id === 3 ? detail(3, [userMsg(21, 'What is prior written notice?'), { ...answer, id: 22 }]) : detail(id, []),
      ),
    );
    act(() => {
      stream.handlers.onDone({
        messageId: 22,
        contentMarkdown: answer.contentMarkdown,
        citations: answer.citations,
        suggestions: answer.suggestions,
        truncated: true,
        disclaimer: 'Not legal advice.',
      });
      stream.resolve();
    });

    const assistant = await screen.findByTestId('advocate-assistant-message');
    expect(screen.queryByTestId('advocate-user-message-pending')).not.toBeInTheDocument();
    expect(screen.queryByTestId('advocate-streaming-text')).not.toBeInTheDocument();
    // The finished answer is announced once through the sr-only status node —
    // the only place a screen reader hears it, since the streaming bubble was hidden.
    // Announced as plain words (no markdown punctuation), from a node outside the conversation region.
    expect(screen.getByTestId('advocate-announcement')).toHaveTextContent('Prior written notice is the letter the school must send.');
    expect(screen.getByTestId('advocate-announcement').closest('[data-testid="advocate-messages"]')).toBeNull();
    expect(within(assistant).getByText('letter')).toBeInTheDocument();
    const sources = within(assistant).getAllByTestId('advocate-source-link');
    expect(sources.map((a) => [a.textContent, a.getAttribute('href')])).toEqual([
      ['Prior written notice', '/knowledge-base/3'],
      ['Child profile', '/children/4/overview'],
    ]);
    expect(within(assistant).queryByTestId('advocate-source-chip')).not.toBeInTheDocument();
    expect(within(assistant).getByTestId('advocate-assistant-message-truncated')).toHaveTextContent(TRUNCATED_NOTICE_COPY);
    expect(within(assistant).getByTestId('advocate-suggestion-prep_question')).toBeInTheDocument();
    expect(within(assistant).getByTestId('advocate-suggestion-journal_entry')).toBeInTheDocument();
    expect(within(assistant).getByRole('link', { name: 'Open the guide' })).toHaveAttribute('href', '/knowledge-base/3');
    expect(within(assistant).getByRole('link', { name: 'See goals' })).toHaveAttribute('href', '/children/4/goals');
    expect(screen.getByTestId('advocate-disclaimer')).toHaveTextContent('Not legal advice.');
    expect(screen.getAllByTestId('advocate-disclaimer')).toHaveLength(1);
    expect(api.getAdvocateUsage).toHaveBeenCalledTimes(2);
    expect(screen.getByTestId('location')).toHaveTextContent('/children/4/advocate?thread=3');
    expect(screen.getByTestId('location')).not.toHaveTextContent('about=');

    // The launcher context goes with the first message only.
    expect(composer()).not.toHaveAttribute('readonly');
    typeAndSend('And what if they refuse?');
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(2));
    expect(lastStream().body).toEqual({ text: 'And what if they refuse?' });
    expect(api.createAdvocateThread).toHaveBeenCalledTimes(1);

    // One-shot: switching to another thread and back mounts an empty status node (thread 3 keeps its
    // stored answer on refetch, so the list renders but nothing is re-announced).
    api.listAdvocateThreads.mockResolvedValue({ success: true, data: [thread(3, 'What is prior written notice?'), thread(1, 'PWN question'), thread(2, 'ETR timing')] });
    fireEvent.click(await screen.findByTestId('advocate-thread-2-open'));
    await screen.findByTestId('advocate-empty');
    fireEvent.click(await screen.findByTestId('advocate-thread-3-open'));
    await screen.findByTestId('advocate-assistant-message');
    expect(screen.getByTestId('advocate-announcement')).toBeEmptyDOMElement();
  });

  it('keeps focus in the composer while the first thread is being created', async () => {
    let resolveCreate: (value: { success: true; data: AdvocateThreadDto }) => void = () => {};
    api.createAdvocateThread.mockImplementation(
      () =>
        new Promise<{ success: true; data: AdvocateThreadDto }>((resolve) => {
          resolveCreate = resolve;
        }),
    );
    renderPage();
    await screen.findByTestId('advocate-empty');

    const el = composer();
    el.focus();
    fireEvent.change(el, { target: { value: 'What is prior written notice?' } });
    fireEvent.keyDown(el, { key: 'Enter' });

    await waitFor(() => expect(api.createAdvocateThread).toHaveBeenCalled());
    // readOnly, not disabled — disabling the textarea would blur it.
    expect(composer()).toHaveAttribute('readonly');
    expect(composer()).not.toBeDisabled();
    expect(composer()).toHaveFocus();
    expect(screen.getByTestId('advocate-send')).toBeDisabled();
    expect(screen.queryByTestId('advocate-stop')).not.toBeInTheDocument();

    act(() => resolveCreate({ success: true, data: thread(3, 'What is prior written notice?') }));
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
  });

  it('keeps the user message and offers Retry when an error frame arrives after deltas', async () => {
    renderPage('owner', '/children/4/advocate?thread=2');
    await waitFor(() => expect(api.getAdvocateThread).toHaveBeenCalledWith(2));
    await screen.findByTestId('advocate-empty');

    typeAndSend('Is the reading goal measurable?');
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
    const stream = lastStream();
    act(() => stream.handlers.onDelta('Looking at'));
    act(() => {
      stream.handlers.onError({ code: 'unavailable', message: 'The advocate could not answer right now.' });
      stream.resolve();
    });

    const error = await screen.findByTestId('advocate-send-error');
    expect(error).toHaveTextContent('The advocate could not answer right now.');
    expect(screen.getByTestId('advocate-user-message-pending')).toHaveTextContent('Is the reading goal measurable?');
    expect(screen.queryByTestId('advocate-streaming-text')).not.toBeInTheDocument();
    expect(composer()).not.toHaveAttribute('readonly');

    // Retry counts as a send for pin-to-bottom. It only does so because `retry` hands `start` a
    // fresh object: passing the pending message already in state makes React bail out of
    // `setPending`, its identity never changes, and the effects keyed on it never re-arm — so a
    // reader who scrolled away while the error showed would not be followed for the retried answer.
    const targets: Element[] = [];
    const spy = vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(function (this: Element) {
      targets.push(this);
    });
    try {
      const scroller = screen.getByTestId('advocate-messages');
      Object.defineProperty(scroller, 'scrollHeight', { value: 2000, configurable: true });
      Object.defineProperty(scroller, 'clientHeight', { value: 500, configurable: true });
      scroller.scrollTop = 0;
      fireEvent.scroll(scroller);

      targets.length = 0;
      fireEvent.click(screen.getByTestId('advocate-retry'));
      await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(2));
      expect(targets).toContain(screen.getByTestId('advocate-scroll-tail'));
    } finally {
      spy.mockRestore();
    }

    expect(lastStream().body).toEqual({ text: 'Is the reading goal measurable?' });
    expect(screen.queryByTestId('advocate-send-error')).not.toBeInTheDocument();
    expect(screen.getByTestId('advocate-user-message-pending')).toBeInTheDocument();
  });

  it('locks the composer and shows the subscription call-to-action when the server answers 429 usage_cap', async () => {
    api.getAdvocateUsage.mockResolvedValue({ success: true, data: { used: 19, limit: 20, subscriptionActive: false } });
    renderPage('owner', '/children/4/advocate?thread=2');
    await screen.findByTestId('advocate-usage-warning');

    typeAndSend('One more question');
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
    await act(async () => {
      lastStream().reject(new AdvocateRequestError(429, 'usage_cap', 'You have used all of this year’s advocate messages.'));
      await Promise.resolve();
    });

    await screen.findByTestId('advocate-usage-capped');
    expect(screen.getByTestId('subscribe-button')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Have an invite code?' })).toHaveAttribute('href', '/redeem-invite');
    expect(composer()).toBeDisabled();
    // The cap is the third leg of the composer's `tinted` OR (streaming || creating || disabled);
    // the other two are covered by the tint test above.
    expect(composer().parentElement).toHaveClass('bg-brand-slate-50');
    expect(composer().parentElement).not.toHaveClass('bg-white');
    expect(screen.getByTestId('advocate-send')).toBeDisabled();
    expect(screen.queryByTestId('advocate-user-message-pending')).not.toBeInTheDocument();
    expect(screen.getByTestId('advocate-send-error')).toHaveTextContent('You have used all of this year’s advocate messages.');
    expect(screen.queryByTestId('advocate-retry')).not.toBeInTheDocument();
  });

  it('shows the allowance banner from 80 %', async () => {
    api.getAdvocateUsage.mockResolvedValue({ success: true, data: { used: 240, limit: 300, subscriptionActive: true } });
    renderPage();
    expect(await screen.findByTestId('advocate-usage-warning')).toHaveTextContent(
      "You've used 240 of 300 advocate messages this year",
    );
    expect(composer()).not.toBeDisabled();
  });

  it('opens, renames and deletes conversations from the rail', async () => {
    renderPage();
    const rail = await screen.findByTestId('advocate-thread-list');
    expect(within(rail).getByTestId('advocate-thread-1')).toHaveTextContent('PWN question');

    fireEvent.click(within(rail).getByTestId('advocate-thread-1-open'));
    await waitFor(() => expect(api.getAdvocateThread).toHaveBeenCalledWith(1));
    expect(await screen.findByTestId('advocate-assistant-message')).toBeInTheDocument();
    expect(screen.getByTestId('advocate-user-message')).toHaveTextContent('What is PWN?');
    expect(within(rail).getByTestId('advocate-thread-1-open')).toHaveAttribute('aria-current', 'true');

    fireEvent.click(within(rail).getByTestId('advocate-thread-1-menu'));
    fireEvent.click(await screen.findByTestId('advocate-thread-1-rename'));
    const input = await screen.findByTestId('advocate-rename-input');
    fireEvent.change(input, { target: { value: 'Prior written notice' } });
    fireEvent.click(screen.getByTestId('advocate-rename-save'));
    await waitFor(() => expect(api.renameAdvocateThread).toHaveBeenCalledWith(1, 'Prior written notice'));
    await waitFor(() => expect(within(rail).getByTestId('advocate-thread-1')).toHaveTextContent('Prior written notice'));
    await waitFor(() => expect(screen.queryByTestId('advocate-rename-form')).not.toBeInTheDocument());

    fireEvent.click(within(rail).getByTestId('advocate-thread-1-menu'));
    fireEvent.click(await screen.findByTestId('advocate-thread-1-delete'));
    const dialog = await screen.findByTestId('advocate-delete-dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete conversation' }));
    await waitFor(() => expect(api.deleteAdvocateThread).toHaveBeenCalledWith(1));
    await waitFor(() => expect(within(rail).queryByTestId('advocate-thread-1')).not.toBeInTheDocument());
    // The open conversation was the deleted one — back to a blank page.
    expect(await screen.findByTestId('advocate-empty')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/children/4/advocate');
    expect(screen.getByTestId('location')).not.toHaveTextContent('thread=');

    fireEvent.click(within(rail).getByTestId('advocate-new-thread'));
    expect(screen.getByTestId('advocate-empty')).toBeInTheDocument();
    expect(api.createAdvocateThread).not.toHaveBeenCalled();
  });

  it('Stop aborts the fetch, keeps the question and unlocks the composer', async () => {
    renderPage('owner', '/children/4/advocate?thread=2');
    await screen.findByTestId('advocate-empty');
    typeAndSend('Tell me about ESY');
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
    const stream = lastStream();
    act(() => stream.handlers.onDelta('Extended school year'));

    fireEvent.click(screen.getByTestId('advocate-stop'));
    expect(stream.handlers.signal?.aborted).toBe(true);
    await screen.findByTestId('advocate-stopped');
    expect(screen.queryByTestId('advocate-streaming-text')).not.toBeInTheDocument();
    expect(screen.getByTestId('advocate-user-message-pending')).toHaveTextContent('Tell me about ESY');
    expect(composer()).not.toHaveAttribute('readonly');
    expect(screen.getByTestId('advocate-send')).toBeInTheDocument();
    expect(screen.queryByTestId('advocate-send-error')).not.toBeInTheDocument();
  });

  it('clears streaming/pending state abandoned by a thread switch, so reopening the thread does not show a frozen stream', async () => {
    renderPage('owner', '/children/4/advocate?thread=2');
    await screen.findByTestId('advocate-empty');
    typeAndSend('Tell me about ESY');
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
    const stream = lastStream();
    act(() => stream.handlers.onDelta('Extended school year'));
    expect(screen.getByTestId('advocate-streaming-text')).toHaveTextContent('Extended school year');

    const rail = screen.getByTestId('advocate-thread-list');
    fireEvent.click(within(rail).getByTestId('advocate-thread-1-open'));
    await screen.findByTestId('advocate-assistant-message');

    fireEvent.click(within(rail).getByTestId('advocate-thread-2-open'));
    await screen.findByTestId('advocate-empty');
    expect(screen.queryByTestId('advocate-streaming-text')).not.toBeInTheDocument();
    expect(screen.queryByTestId('advocate-user-message-pending')).not.toBeInTheDocument();
    expect(screen.queryByTestId('advocate-stop')).not.toBeInTheDocument();
    fireEvent.change(composer(), { target: { value: 'A fresh question' } });
    expect(screen.getByTestId('advocate-send')).toBeEnabled();
  });

  it('sends on Enter but not on Shift+Enter, and blocks over-length text', async () => {
    renderPage('owner', '/children/4/advocate?thread=2');
    await screen.findByTestId('advocate-empty');

    fireEvent.change(composer(), { target: { value: 'Line one' } });
    fireEvent.keyDown(composer(), { key: 'Enter', shiftKey: true });
    expect(api.streamAdvocateMessage).not.toHaveBeenCalled();

    fireEvent.change(composer(), { target: { value: 'x'.repeat(2001) } });
    expect(screen.getByTestId('advocate-composer-count')).toHaveTextContent('2,001 / 2,000 — too long');
    fireEvent.keyDown(composer(), { key: 'Enter' });
    expect(api.streamAdvocateMessage).not.toHaveBeenCalled();
    expect(screen.getByTestId('advocate-send')).toBeDisabled();

    fireEvent.change(composer(), { target: { value: 'Line one' } });
    fireEvent.keyDown(composer(), { key: 'Enter' });
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
    expect(lastStream().body.text).toBe('Line one');
  });

  it('hides the composer and thread actions from a viewer, but still shows a read-only thread and its disclaimer', async () => {
    renderPage('viewer');
    await screen.findByTestId('advocate-thread-list');
    expect(screen.getByTestId('advocate-viewer-notice')).toBeInTheDocument();
    expect(screen.queryByTestId('advocate-composer')).not.toBeInTheDocument();
    expect(screen.queryByTestId('advocate-new-thread')).not.toBeInTheDocument();
    expect(screen.queryByTestId('advocate-example')).not.toBeInTheDocument();
    expect(screen.queryByTestId('advocate-thread-1-menu')).not.toBeInTheDocument();

    // Regression guard: a read-only collaborator can still open an existing thread and must still
    // see its disclaimer — the message-region regrouping around `showDisclaimer` must not drop it
    // just because the composer (and its dock) never render for a viewer.
    fireEvent.click(screen.getByTestId('advocate-thread-1-open'));
    await screen.findByTestId('advocate-assistant-message');
    expect(screen.getByTestId('advocate-disclaimer')).toHaveTextContent('Not legal advice.');
  });

  it('hands suggestions to existing flows: journal drawer prefilled, prep question copied with a toast', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    renderPage('owner', '/children/4/advocate?thread=1');
    const assistant = await screen.findByTestId('advocate-assistant-message');

    fireEvent.click(within(assistant).getByRole('button', { name: 'Add to journal' }));
    const form = await screen.findByTestId('advocate-journal-drawer-form');
    expect(within(form).getByLabelText('What happened')).toHaveValue('School said no to the evaluation on 9/10.');
    expect(within(form).getByLabelText('Date')).toHaveValue('2026-09-10');

    fireEvent.click(within(assistant).getByRole('button', { name: 'Copy' }));
    expect(writeText).toHaveBeenCalledWith('When will I get prior written notice about this change?');
    await waitFor(() => expect(toast.show).toHaveBeenCalledWith({ message: PREP_QUESTION_COPIED_TOAST, variant: 'success' }));
  });

  it('hands a prep question to meeting prep through ?addQuestion=', async () => {
    renderPage('owner', '/children/4/advocate?thread=1');
    const assistant = await screen.findByTestId('advocate-assistant-message');
    fireEvent.click(within(assistant).getByRole('button', { name: 'Add to meeting prep' }));
    expect(await screen.findByTestId('meeting-prep-page')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(
      '/children/4/meeting-prep?addQuestion=When+will+I+get+prior+written+notice+about+this+change%3F',
    );
  });

  it('deep-links every kind of source chip and reuses a cited goal for open_goal', async () => {
    api.getAdvocateThread.mockImplementation((id: number) =>
      Promise.resolve(
        detail(id, [
          userMsg(11, 'Is the reading goal measurable?'),
          {
            ...answer,
            citations: [
              { kind: 'goal', id: 340, label: 'Reading goal', parent: { kind: 'iep', id: 12 } },
              { kind: 'journal', id: 77, label: 'Journal entry 2026-09-12' },
              { kind: 'comparison', id: 1, label: 'IEP comparison' },
              { kind: 'mystery', id: 5, label: 'Something new' },
            ],
            suggestions: [{ kind: 'open_goal', id: 340, text: 'Look at the reading goal' }],
          },
        ]),
      ),
    );
    renderPage('owner', '/children/4/advocate?thread=1');
    const assistant = await screen.findByTestId('advocate-assistant-message');
    expect(within(assistant).getByRole('link', { name: 'Reading goal' })).toHaveAttribute('href', '/children/4/ieps/12#goal-340');
    expect(within(assistant).getByRole('link', { name: 'Journal entry 2026-09-12' })).toHaveAttribute(
      'href',
      '/children/4/journal?entry=77',
    );
    const chips = within(assistant).getAllByTestId('advocate-source-chip');
    expect(chips.map((c) => c.textContent)).toEqual(['IEP comparison', 'Something new']);
    expect(within(assistant).getByRole('link', { name: 'Open the goal' })).toHaveAttribute('href', '/children/4/ieps/12#goal-340');
  });

  it('shows the context pill for ?about=, sends it once with a fresh thread, and a new launcher starts another thread', async () => {
    renderPage('owner', '/children/4/advocate?about=iep:12', { to: '/children/4/advocate?about=etr:5', label: 'ETR from Sep 1, 2026' });
    await screen.findByTestId('advocate-empty');
    expect(screen.getByTestId('advocate-about-pill')).toHaveTextContent('About: this IEP');
    expect(screen.getByTestId('advocate-about-pill')).not.toHaveTextContent('iep:12');

    typeAndSend('Is this IEP complete?');
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
    expect(lastStream().body).toEqual({ text: 'Is this IEP complete?', about: 'iep:12' });
    expect(api.createAdvocateThread).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/children/4/advocate?thread=3'));
    expect(screen.queryByTestId('advocate-about-pill')).not.toBeInTheDocument();
    act(() => {
      lastStream().handlers.onDone({
        messageId: 30,
        contentMarkdown: 'Mostly.',
        citations: [],
        suggestions: [],
        truncated: false,
        disclaimer: 'Not legal advice.',
      });
      lastStream().resolve();
    });
    await waitFor(() => expect(composer()).not.toHaveAttribute('readonly'));

    // A launcher on another page lands here while the page is still mounted.
    api.createAdvocateThread.mockImplementation((_childId: number, title?: string) =>
      Promise.resolve({ success: true, data: thread(7, title ?? 'New conversation') }),
    );
    fireEvent.click(screen.getByTestId('fake-launcher'));
    expect(await screen.findByTestId('advocate-empty')).toBeInTheDocument();
    expect(screen.getByTestId('advocate-about-pill')).toHaveTextContent('About: ETR from Sep 1, 2026');

    typeAndSend('What did the ETR find?');
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(2));
    expect(api.createAdvocateThread).toHaveBeenCalledTimes(2);
    expect(lastStream().threadId).toBe(7);
    expect(lastStream().body).toEqual({ text: 'What did the ETR find?', about: 'etr:5' });
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/children/4/advocate?thread=7'));
  });

  it('drops the launcher context when the parent opens an existing conversation instead', async () => {
    renderPage('owner', '/children/4/advocate?about=iep:12');
    await screen.findByTestId('advocate-about-pill');
    fireEvent.click(await screen.findByTestId('advocate-thread-2-open'));
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/children/4/advocate?thread=2'));
    expect(screen.getByTestId('location')).not.toHaveTextContent('about=');
    expect(screen.queryByTestId('advocate-about-pill')).not.toBeInTheDocument();
    await screen.findByTestId('advocate-empty');
    typeAndSend('Hello');
    await waitFor(() => expect(api.streamAdvocateMessage).toHaveBeenCalledTimes(1));
    expect(lastStream().body).toEqual({ text: 'Hello' });
  });

  it('nudges a parent when no state resolves for the child and remembers the dismissal', async () => {
    auth.user = parent(null);
    api.getAdvocateChildContext.mockResolvedValue({ success: true, data: { stateCode: null } });
    const first = renderPage();
    const hint = await screen.findByTestId('advocate-state-hint');
    expect(within(hint).getByRole('link', { name: STATE_HINT_COPY })).toHaveAttribute('href', '/profile');
    fireEvent.click(within(hint).getByRole('button', { name: 'Dismiss this hint' }));
    expect(screen.queryByTestId('advocate-state-hint')).not.toBeInTheDocument();
    first.unmount();

    const second = renderPage();
    await screen.findByTestId('advocate-empty');
    expect(screen.queryByTestId('advocate-state-hint')).not.toBeInTheDocument();
    second.unmount();

    // The server resolved a state (e.g. from a linked district) even though the profile is blank: no hint.
    auth.user = parent(null);
    localStorage.clear();
    api.getAdvocateChildContext.mockResolvedValue({ success: true, data: { stateCode: 'OH' } });
    renderPage();
    await screen.findByTestId('advocate-empty');
    expect(screen.queryByTestId('advocate-state-hint')).not.toBeInTheDocument();
  });

  it('does not show the state hint when the context call fails — a failure is not the server positively saying "no state"', async () => {
    auth.user = parent(null);
    api.getAdvocateChildContext.mockRejectedValue(new Error('network'));
    renderPage();
    await screen.findByTestId('advocate-empty');
    expect(screen.queryByTestId('advocate-state-hint')).not.toBeInTheDocument();
  });

  it('offers the journal example only when the child has journal entries', async () => {
    journalApi.listJournalEntries.mockResolvedValue({ success: true, data: [{ id: 1 }] });
    renderPage();
    await waitFor(() =>
      expect(screen.getAllByTestId('advocate-example').map((b) => b.textContent)).toEqual([...EXAMPLE_QUESTIONS, JOURNAL_EXAMPLE_QUESTION]),
    );
    expect(journalApi.listJournalEntries).toHaveBeenCalledWith(4, { take: 1 });
  });
});
