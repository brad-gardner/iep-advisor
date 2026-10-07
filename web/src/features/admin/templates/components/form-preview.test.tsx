import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
// `admin` is a staff-only namespace (plan phase 6) — its English isn't
// bundled in `resources` (see `lib/i18n/index.ts`), only registered by this
// side-effect import, exactly as the page's real lazy route chunk
// (`app/lazy-routes/platform-admin-routes.tsx`) registers it before the page
// can render. See `docs/i18n/README.md`'s "Staff and admin namespaces".
import '@/app/lazy-routes/staff-locales';
import { FormPreview } from './form-preview';
import type { TemplateSectionDto } from '../types';

function sections(): TemplateSectionDto[] {
  return [
    {
      id: 1,
      sectionKey: 'narrative',
      title: 'Present Levels',
      displayOrder: 0,
      fields: [
        {
          id: 1,
          fieldKey: 'notes',
          fieldType: 'RichText',
          label: 'Notes',
          required: false,
          configJson: null,
          displayOrder: 0,
        },
      ],
    },
  ];
}

describe('FormPreview', () => {
  it('previews a RichText field as a disabled rich text editor matching the field label', () => {
    render(<FormPreview sections={sections()} />);

    const editor = screen.getByLabelText('Notes');
    expect(editor).toBeDisabled();
  });

  it('shows a placeholder message when there are no sections', () => {
    render(<FormPreview sections={[]} />);
    expect(screen.getByText('Add a section to see the form preview.')).toBeInTheDocument();
  });
});
