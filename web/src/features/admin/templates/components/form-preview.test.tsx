import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
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
