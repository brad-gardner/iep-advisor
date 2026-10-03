import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import type { AutosaveStatus } from '@/hooks/use-autosave';
import type { DocumentInstance, SaveResult } from '../hooks/use-document-instance';
import type { DocumentInstanceDetailDto, DocumentValuePatch } from '../types';
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

  const saveValues = async (patch: DocumentValuePatch): Promise<SaveResult> => {
    setSaveStatus('saving');
    const result = onSave ? await onSave(patch) : { ok: true, values: patch };
    if (result.ok) {
      setDetail((d) => ({ ...d, values: { ...d.values, ...patch } }));
    }
    setSaveStatus(result.ok ? 'saved' : 'error');
    return result;
  };

  const instance: Pick<DocumentInstance, 'saveStatus' | 'conflict' | 'reloadKey' | 'readOnly' | 'saveValues' | 'reload' | 'getSaveState'> = {
    saveStatus,
    conflict: false,
    reloadKey: 0,
    readOnly: false,
    saveValues,
    reload: () => {},
    getSaveState: () => ({ hasError: false, conflict: false, pending: false }),
  };

  return (
    <ToastProvider>
      <DocumentEditor detail={detail} instance={instance} />
    </ToastProvider>
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
});
