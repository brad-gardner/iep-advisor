import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AuthoredVersionSnapshot } from './authored-version-snapshot';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';

function templateVersion(): TemplateVersionDetailDto {
  return {
    id: 1,
    documentTemplateId: 1,
    versionNumber: 1,
    status: 'Published',
    publishedAt: '2026-01-01T00:00:00Z',
    rowVersion: null,
    sections: [
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
          {
            id: 2,
            fieldKey: 'shortName',
            fieldType: 'Text',
            label: 'Short name',
            required: false,
            configJson: null,
            displayOrder: 1,
          },
        ],
      },
    ],
  };
}

describe('AuthoredVersionSnapshot', () => {
  it('renders a frozen RichText value as formatted markdown', () => {
    render(
      <AuthoredVersionSnapshot
        templateVersion={templateVersion()}
        values={{ notes: 'Reads **60 wpm** with:\n\n- support\n- prompting', shortName: 'JE' }}
      />
    );

    const notesField = screen.getByTestId('snapshot-field-notes');
    expect(notesField.querySelector('strong')).toHaveTextContent('60 wpm');
    expect(notesField.querySelector('li')).toHaveTextContent('support');
  });

  it('still renders a Text value literally, without parsing markdown syntax', () => {
    render(
      <AuthoredVersionSnapshot
        templateVersion={templateVersion()}
        values={{ notes: 'plain', shortName: '**JE**' }}
      />
    );

    const shortNameField = screen.getByTestId('snapshot-field-shortName');
    expect(shortNameField.querySelector('strong')).not.toBeInTheDocument();
    expect(shortNameField).toHaveTextContent('**JE**');
  });
});
