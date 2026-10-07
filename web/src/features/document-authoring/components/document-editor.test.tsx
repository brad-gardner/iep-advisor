import { useRef, useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { AutosaveStatus } from '@/hooks/use-autosave';
import type { DocumentInstance, SaveResult } from '../hooks/use-document-instance';
import type { AuthoredDocumentVersionSummaryDto, DocumentInstanceDetailDto, DocumentValuePatch } from '../types';
// `document-authoring` is a staff-only namespace (plan phase 5) — see
// `../staff-locales`'s doc comment and `docs/i18n/README.md`'s "Staff and
// admin namespaces". This component renders directly here (not through the
// lazy route), so its English must be registered the same way the real
// route chunk does.
import '../staff-locales';
import { DocumentEditor } from './document-editor';

// Every API this page's descendants touch on mount, stubbed to inert/empty
// responses so the test exercises only the Phase 1 page-frame + section
// behavior (SharedBanner, ShareWithFamilyButton, ProposedEditsPanel, the
// Finalize version-number hint), not any of those features themselves.
const draftSharingApi = vi.hoisted(() => ({
  getSharePreview: vi.fn().mockResolvedValue({ success: true, data: { recipients: [], policyEnabled: false, lastSharedAt: null, willSupersedeRevision: null } }),
  getShares: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('@/features/draft-sharing/api/draft-sharing-api', () => draftSharingApi);

const meetingDecisionsApi = vi.hoisted(() => ({
  getProposedEdits: vi.fn().mockResolvedValue({ success: true, data: [] }),
  markDecisionApplied: vi.fn(),
}));
vi.mock('@/features/meetings/api/meeting-decisions-api', () => meetingDecisionsApi);

const shareableEntriesApi = vi.hoisted(() => ({
  getEducatorShareableEntries: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('@/features/student/api/shareable-entries-api', () => shareableEntriesApi);

// useStudentTeam (plan 2026-10-02-002) fetches the student's team eagerly on
// mount — unlike the lazy shareableEntries cache above.
const educatorApi = vi.hoisted(() => ({
  getTeam: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('@/features/educator/api/educator-api', () => educatorApi);

const documentsApi = vi.hoisted(() => ({
  listAuthoredVersions: vi.fn().mockResolvedValue({ success: true, data: [] }),
  finalizeDocument: vi.fn(),
}));
vi.mock('../api/documents-api', () => documentsApi);

const PROFILE_FIELD = 'profile-field';
const PRESENT_FIELD = 'present-field';

function makeDetail(values: Record<string, unknown>): DocumentInstanceDetailDto {
  return {
    id: 1,
    schoolStudentId: 2,
    documentTypeId: 3,
    documentTypeKey: 'iep',
    documentTypeDisplayName: 'IEP',
    documentTemplateVersionId: 4,
    status: 'Draft',
    values,
    rowVersion: 'v1',
    createdAt: '2026-01-01T00:00:00Z',
    lastEditedAt: null,
    lastEditedByUserId: null,
    amendsVersionId: null,
    amendsVersionNumber: null,
    amendmentReason: null,
    effectiveDate: null,
    templateVersion: {
      id: 4,
      documentTemplateId: 3,
      versionNumber: 1,
      status: 'Published',
      publishedAt: '2026-01-01T00:00:00Z',
      rowVersion: null,
      sections: [
        {
          id: 1,
          sectionKey: 'profile',
          title: 'Student Profile',
          displayOrder: 0,
          fields: [{ id: 100, fieldKey: PROFILE_FIELD, fieldType: 'Text', label: 'Profile', required: true, displayOrder: 0, configJson: null }],
        },
        {
          id: 2,
          sectionKey: 'present',
          title: 'Present Levels',
          displayOrder: 1,
          fields: [{ id: 101, fieldKey: PRESENT_FIELD, fieldType: 'RichText', label: 'Present levels', required: false, displayOrder: 0, configJson: null }],
        },
      ],
    },
  };
}

function finalizedVersion(): AuthoredDocumentVersionSummaryDto {
  return {
    id: 99,
    schoolStudentId: 2,
    documentTypeId: 3,
    documentTypeKey: 'iep',
    documentTypeDisplayName: 'IEP',
    versionNumber: 1,
    finalizedByUserId: 1,
    finalizedAt: '2026-01-01T00:00:00Z',
    pdfRenderStatus: 'Pending',
    signatureStatus: 'Unsigned',
    signedArtifactCount: 0,
    amendsVersionId: null,
    amendsVersionNumber: null,
    amendmentReason: null,
    effectiveDate: null,
    amendedByVersionIds: [],
  };
}

/** Deferred promise so a test can hold a save "in flight" and resolve it on cue. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((res) => {
    resolve = res;
  });
  return { promise, resolve };
}

/**
 * Stands in for `useDocumentInstance`: owns `detail.values` and `saveStatus`
 * the same way the real hook does, so a Done round-trips through to the read
 * view and the header/strip's save-state pill reflects real saves — without
 * hitting the network.
 */
function Harness({
  initialValues,
  onSave,
}: {
  initialValues: Record<string, unknown>;
  /** Intercept a save before it "lands" — return a promise to hold it in flight. */
  onSave?: (patch: DocumentValuePatch) => Promise<SaveResult> | SaveResult;
}) {
  const [detail, setDetail] = useState(() => makeDetail(initialValues));
  const [saveStatus, setSaveStatus] = useState<AutosaveStatus>('idle');
  // Mirrors use-document-instance.ts's own pendingRef/errorRef: `getSaveState()`
  // must reflect the latest settled outcome synchronously (read right after an
  // `await flushAll()`), which React state read through this closure cannot.
  const pendingRef = useRef(0);
  const errorRef = useRef(false);

  const saveValues = async (patch: DocumentValuePatch): Promise<SaveResult> => {
    pendingRef.current += 1;
    setSaveStatus('saving');
    const result = onSave ? await onSave(patch) : { ok: true, values: patch };
    if (result.ok) {
      setDetail((d) => ({ ...d, values: { ...d.values, ...patch } }));
    }
    errorRef.current = !result.ok;
    pendingRef.current -= 1;
    if (pendingRef.current === 0) setSaveStatus(result.ok ? 'saved' : 'error');
    return result;
  };

  const instance: Pick<DocumentInstance, 'saveStatus' | 'conflict' | 'reloadKey' | 'readOnly' | 'saveValues' | 'reload' | 'getSaveState'> = {
    saveStatus,
    conflict: false,
    reloadKey: 0,
    readOnly: false,
    saveValues,
    reload: () => {},
    getSaveState: () => ({ hasError: errorRef.current, conflict: false, pending: pendingRef.current > 0 }),
  };

  return (
    // A successful Finalize renders a <Link> to the new version — needs a
    // router context even though this harness never navigates.
    <MemoryRouter>
      <ToastProvider>
        <DocumentEditor detail={detail} instance={instance} />
      </ToastProvider>
    </MemoryRouter>
  );
}

describe('DocumentEditor', () => {
  it('renders the page frame: header, completeness strip, section navigator, and every section read-only', async () => {
    render(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan is a 4th grader.', [PRESENT_FIELD]: '' }} />);

    expect(screen.getByRole('heading', { name: 'IEP', level: 1 })).toBeInTheDocument();
    expect(await screen.findByTestId('completeness-strip')).toBeInTheDocument();
    expect(screen.getByTestId('section-navigator')).toBeInTheDocument();
    expect(screen.getByTestId('section-1-edit')).toBeInTheDocument();
    expect(screen.getByTestId('section-2-edit')).toBeInTheDocument();
    expect(screen.getByTestId('read-field-profile-field')).toHaveTextContent('Jordan is a 4th grader.');
  });

  it('two sections can be open at the same time', async () => {
    const user = userEvent.setup();
    render(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }} />);

    await user.click(screen.getByTestId('section-1-edit'));
    await user.click(screen.getByTestId('section-2-edit'));

    expect(screen.getByTestId('section-1-done')).toBeInTheDocument();
    expect(screen.getByTestId('section-2-done')).toBeInTheDocument();
    expect(screen.getByTestId(`field-${PROFILE_FIELD}`)).toBeInTheDocument();
    expect(screen.getByTestId(`field-${PRESENT_FIELD}`)).toBeInTheDocument();
  });

  it('Done saves the edit and the section returns to its read view with the new value', async () => {
    const user = userEvent.setup();
    render(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }} />);

    await user.click(screen.getByTestId('section-1-edit'));
    await user.clear(screen.getByTestId(`field-${PROFILE_FIELD}`));
    await user.type(screen.getByTestId(`field-${PROFILE_FIELD}`), 'Jordan Ellis');
    await user.click(screen.getByTestId('section-1-done'));

    await waitFor(() => expect(screen.getByTestId('read-field-profile-field')).toHaveTextContent('Jordan Ellis'));
    expect(screen.getByTestId('section-1-edit')).toBeInTheDocument();
  });

  it('shows "updating…" on the completeness strip while an open section has a save in flight', async () => {
    const user = userEvent.setup();
    const gate = deferred<SaveResult>();
    render(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }} onSave={() => gate.promise} />);

    await user.click(screen.getByTestId('section-1-edit'));
    await user.type(screen.getByTestId(`field-${PROFILE_FIELD}`), '!');
    await user.click(screen.getByTestId('section-1-done')); // Done flushes immediately, without waiting for the debounce

    expect(await screen.findByTestId('completeness-updating')).toBeInTheDocument();

    gate.resolve({ ok: true, values: { [PROFILE_FIELD]: 'Jordan!' } });
    await waitFor(() => expect(screen.queryByTestId('completeness-updating')).not.toBeInTheDocument());
  });

  it('keyboard: "]" moves the active section and "E" opens it for editing', async () => {
    const user = userEvent.setup();
    render(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }} />);

    // Focus something outside any input so the shortcut handler doesn't bail
    // out on an editable target.
    document.body.focus();
    await user.keyboard(']');
    await waitFor(() => expect(screen.getByTestId('section-nav-2')).toHaveAttribute('aria-current', 'location'));

    await user.keyboard('e');
    expect(await screen.findByTestId('section-2-done')).toBeInTheDocument();
  });

  it('the assistant opens in a drawer and closes again', async () => {
    const user = userEvent.setup();
    render(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }} />);

    expect(screen.queryByTestId('chat-panel')).not.toBeInTheDocument();
    await user.click(screen.getByTestId('document-chat-open'));
    expect(await screen.findByTestId('chat-panel')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Assistant' })).toBeInTheDocument();

    await user.click(screen.getByTestId('document-chat-open'));
    await waitFor(() => expect(screen.queryByTestId('chat-panel')).not.toBeInTheDocument());
  });

  it('does not swallow an "e" keystroke typed into a contenteditable element', async () => {
    render(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }} />);
    // Lets this render's own mount-time fetches (SharedBanner, etc.) settle
    // before the test ends, same as every other (async) test here — this is
    // the only synchronous one, so it would otherwise log their act() warnings.
    await screen.findByTestId('completeness-strip');

    const editable = document.createElement('div');
    // jsdom doesn't implement the `contentEditable` property/`isContentEditable`
    // at all, so set the literal attribute TipTap actually renders instead.
    editable.setAttribute('contenteditable', 'true');
    document.body.appendChild(editable);
    try {
      const event = new KeyboardEvent('keydown', { key: 'e', bubbles: true, cancelable: true });
      editable.dispatchEvent(event);
      expect(event.defaultPrevented).toBe(false);
    } finally {
      document.body.removeChild(editable);
    }
  });

  it('Finalize flushes pending edits in every open section, then closes them, when the flush is clean', async () => {
    const user = userEvent.setup();
    documentsApi.finalizeDocument.mockResolvedValueOnce({ success: true, data: finalizedVersion() });
    render(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }} />);

    // Open and type into section 1 BEFORE opening section 2 — not the
    // "open both, then type into both" order this test used before
    // (todos/246). `SectionCard`'s own open-focus effect schedules a
    // `requestAnimationFrame` that auto-focuses the first field in ITS
    // section's body as soon as `isOpen` flips true (see the comment on
    // that effect, around `hasMountedRef`/`raf` there). That's a REAL timer
    // (jsdom's RAF is not microtask-flushed by `act()`/`user.type()`'s own
    // awaits — see `test/setup.ts`'s note on RAF "surfacing as test-run
    // noise... occasionally after the test that triggered it has already
    // finished"), so opening section 2 while section 1's own RAF (or
    // section 2's own, newly-scheduled one) is still pending left a real
    // window for it to fire mid-keystroke and steal focus from the profile
    // field to the present field, silently dropping the typed "!" (observed
    // once on CI: saved "Jordan" instead of "Jordan!"). Finishing section
    // 1's edit before section 2 even opens means any stray RAF only ever
    // re-focuses an element that's already the right target (a no-op), for
    // either section, at every point in this sequence.
    await user.click(screen.getByTestId('section-1-edit'));
    await user.type(screen.getByTestId(`field-${PROFILE_FIELD}`), '!');
    await user.click(screen.getByTestId('section-2-edit'));
    await user.type(screen.getByTestId(`field-${PRESENT_FIELD}`), 'Doing well.');

    await user.click(screen.getByTestId('finalize-button'));
    await user.click(screen.getByTestId('finalize-confirm'));

    // Everything this test cares about — the mock being called AND the DOM
    // reflecting the post-flush state — is asserted inside ONE waitFor, not
    // split across two sequential ones. `toHaveBeenCalled()` becoming true
    // only proves `finalizeDocument(instanceId)` was reached inside
    // `handleConfirm`; it says nothing about whether the state update from
    // `sectionEditing.closeAll()` (called just before it, in the same async
    // continuation) has actually been FLUSHED to the DOM yet — React can
    // commit that in a later microtask, which only `waitFor`'s own retry
    // loop (not a second, independent `waitFor` starting its own fresh
    // first poll) reliably outlasts. Both edits were flushed (not lost)
    // ahead of the snapshot, and both sections closed once that flush was
    // confirmed clean — each field's save lands via its own independent
    // `setDetail` update, not necessarily in the same commit as the other
    // or as `closeAll`'s, so every assertion below stays in this SAME
    // `waitFor`. A slightly longer-than-default timeout gives this nested
    // two-level `Promise.all` flush chain (document → section → field,
    // times two open sections) a bit more headroom under real CI resource
    // contention (todos/246).
    await waitFor(
      () => {
        expect(documentsApi.finalizeDocument).toHaveBeenCalled();
        expect(screen.getByTestId('read-field-profile-field')).toHaveTextContent('Jordan!');
        expect(screen.getByTestId('read-field-present-field')).toHaveTextContent('Doing well.');
        expect(screen.getByTestId('section-1-edit')).toBeInTheDocument();
        expect(screen.getByTestId('section-2-edit')).toBeInTheDocument();
      },
      { timeout: 2000 }
    );
  });

  it('Finalize does not close sections, or call finalizeDocument, when the flushed save fails', async () => {
    documentsApi.finalizeDocument.mockClear(); // isolate from the preceding success test's call count
    const user = userEvent.setup();
    render(
      <Harness
        initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }}
        onSave={() => ({ ok: false, message: 'Server unavailable.' })}
      />
    );

    await user.click(screen.getByTestId('section-1-edit'));
    await user.type(screen.getByTestId(`field-${PROFILE_FIELD}`), '!');

    await user.click(screen.getByTestId('finalize-button'));
    await user.click(screen.getByTestId('finalize-confirm'));

    const dialog = screen.getByTestId('finalize-document-dialog');
    await waitFor(() => expect(within(dialog).getByRole('alert')).toHaveTextContent('could not be saved'));
    expect(documentsApi.finalizeDocument).not.toHaveBeenCalled();
    expect(screen.getByTestId('section-1-done')).toBeInTheDocument(); // still open, edit not discarded
  });

  it('Finalize stays blocked by a section\'s failed field even after a DIFFERENT field\'s later save resets the document-level error flag', async () => {
    documentsApi.finalizeDocument.mockClear();
    const user = userEvent.setup();
    render(
      <Harness
        initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }}
        onSave={(patch) => (PROFILE_FIELD in patch ? { ok: false, message: 'Server unavailable.' } : { ok: true, values: patch })}
      />
    );

    // Section 1's field fails and stays open...
    await user.click(screen.getByTestId('section-1-edit'));
    await user.type(screen.getByTestId(`field-${PROFILE_FIELD}`), '!');
    await user.click(screen.getByTestId('section-1-done'));
    await waitFor(() => expect(screen.getByTestId(`section-1-retry`)).toBeInTheDocument());

    // ...then section 2's field saves fine, which — at the single
    // last-settled-save `errorRef` use-document-instance.ts keeps — resets the
    // DOCUMENT-level error flag back to false, even though section 1's own
    // field never actually recovered.
    await user.click(screen.getByTestId('section-2-edit'));
    await user.type(screen.getByTestId(`field-${PRESENT_FIELD}`), 'Doing well.');
    await user.click(screen.getByTestId('section-2-done'));
    await waitFor(() => expect(screen.queryByTestId('section-2-done')).not.toBeInTheDocument());

    // Finalize must still refuse — `sectionEditing.hasFailures()` catches what
    // the (now-reset) document-level flag alone would have missed.
    await user.click(screen.getByTestId('finalize-button'));
    await user.click(screen.getByTestId('finalize-confirm'));

    const dialog = screen.getByTestId('finalize-document-dialog');
    await waitFor(() => expect(within(dialog).getByRole('alert')).toHaveTextContent('could not be saved'));
    expect(documentsApi.finalizeDocument).not.toHaveBeenCalled();
    expect(screen.getByTestId('section-1-done')).toBeInTheDocument(); // section 1 still open with its failure
  });

  it('Finalize proceeds once a failed section is Discarded — the restore clears the per-field failure record instead of leaving it for Finalize to trip over', async () => {
    documentsApi.finalizeDocument.mockClear();
    documentsApi.finalizeDocument.mockResolvedValueOnce({ success: true, data: finalizedVersion() });
    const user = userEvent.setup();
    render(
      <Harness
        initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }}
        // Fails only the EDITED value — Discard's restore re-sends the
        // original ('Jordan'), which must succeed.
        onSave={(patch) => (patch[PROFILE_FIELD] === 'Jordan!' ? { ok: false, message: 'Server unavailable.' } : { ok: true, values: patch })}
      />
    );

    await user.click(screen.getByTestId('section-1-edit'));
    await user.type(screen.getByTestId(`field-${PROFILE_FIELD}`), '!');
    await user.click(screen.getByTestId('section-1-done'));
    await waitFor(() => expect(screen.getByTestId('section-1-retry')).toBeInTheDocument());

    // Discard the failed edit instead of retrying it.
    await user.click(screen.getByTestId('section-1-discard'));
    await user.click(await screen.findByTestId('section-1-discard-confirm'));
    await waitFor(() => expect(screen.getByTestId('section-1-edit')).toBeInTheDocument());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    // Finalize must now proceed — the restore cleared section 1's failure
    // record (instead of leaving it blocked until the next reopen).
    await user.click(screen.getByTestId('finalize-button'));
    await user.click(screen.getByTestId('finalize-confirm'));

    await waitFor(() => expect(documentsApi.finalizeDocument).toHaveBeenCalled());
  });

  afterEach(() => resetTestLanguage());

  it('renders the page frame in Spanish', async () => {
    await renderInSpanish(<Harness initialValues={{ [PROFILE_FIELD]: 'Jordan', [PRESENT_FIELD]: '' }} />, {
      ns: 'document-authoring',
    });

    expect(screen.getByText('Borrador')).toBeInTheDocument(); // the status badge, Draft → Borrador
    expect(screen.getByRole('heading', { name: 'Finalizar' })).toBeInTheDocument();
    expect(screen.getByText('Evidencia')).toBeInTheDocument();
    expect(screen.getByText('Preguntar al asistente')).toBeInTheDocument();
  });
});
