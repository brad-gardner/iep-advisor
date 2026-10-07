import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { TemplateSectionDto } from '../types';
import type { SaveResult } from '../hooks/use-document-instance';
// `document-authoring` is a staff-only namespace (plan phase 5) — see
// `../staff-locales`'s doc comment and `docs/i18n/README.md`'s "Staff and
// admin namespaces". This component renders directly here (not through the
// lazy route), so its English must be registered the same way the real
// route chunk does.
import '../staff-locales';

// Stands in for a RichText field's real shape: its formatting toolbar renders
// a focusable `<button>` (e.g. "Bold") BEFORE the editable contenteditable in
// DOM order (see rich-text-editor-impl.tsx's Toolbar, which renders ahead of
// EditorContent). Mocked at the module level — rather than mounting the real,
// lazy-loaded TipTap editor — so this test exercises SectionCard's own
// open-focus query deterministically, against the exact DOM shape that
// previously focused the toolbar button instead of the field (P2-1), without
// depending on the real editor's async chunk-load timing.
vi.mock('./field-renderers/document-field', () => ({
  DocumentField: () => (
    <div>
      <div role="toolbar" aria-label="Formatting">
        <button type="button" aria-label="Bold">
          Bold
        </button>
      </div>
      <div role="textbox" aria-label="Narrative" contentEditable="true" suppressContentEditableWarning />
    </div>
  ),
}));

import { SectionCard } from './section-card';

const FIELD_KEY = 'narrative-field';

function richTextSection(): TemplateSectionDto {
  return {
    id: 40,
    sectionKey: 'narrative',
    title: 'Narrative',
    displayOrder: 0,
    fields: [
      { id: 400, fieldKey: FIELD_KEY, fieldType: 'RichText', label: 'Narrative', required: false, displayOrder: 0, configJson: null },
    ],
  };
}

function Harness({ saveValues }: { saveValues: (patch: Record<string, unknown>) => Promise<SaveResult> }) {
  const [isOpen, setIsOpen] = useState(false);
  return (
    <SectionCard
      section={richTextSection()}
      values={{ [FIELD_KEY]: 'Existing text.' }}
      disabled={false}
      saveValues={saveValues}
      isOpen={isOpen}
      onOpen={() => setIsOpen(true)}
      onClose={() => setIsOpen(false)}
      items={[]}
    />
  );
}

describe('SectionCard — focus on Edit when the first field is RichText-shaped', () => {
  it('focuses the contenteditable, not the toolbar Bold button that precedes it in the DOM', async () => {
    const user = userEvent.setup();
    const saveValues = vi.fn().mockResolvedValue({ ok: true, values: {} });
    render(<Harness saveValues={saveValues} />);

    await user.click(screen.getByTestId('section-40-edit'));

    const editable = screen.getByRole('textbox', { name: 'Narrative' });
    await waitFor(() => expect(document.activeElement).toBe(editable));
    expect(document.activeElement).not.toBe(screen.getByRole('button', { name: 'Bold' }));
  });
});
