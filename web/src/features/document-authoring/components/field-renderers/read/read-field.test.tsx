import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import { ReadField } from './read-field';

function field(overrides: Partial<TemplateFieldDto>): TemplateFieldDto {
  return {
    id: 1,
    fieldKey: 'f-1',
    fieldType: 'Text',
    label: 'Field label',
    required: false,
    displayOrder: 0,
    configJson: null,
    ...overrides,
  };
}

describe('ReadField', () => {
  it('Text: renders the plain-text value, or "Not set" when blank', () => {
    const { rerender } = render(<ReadField field={field({ fieldType: 'Text' })} value="Jordan Ellis" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Jordan Ellis');

    rerender(<ReadField field={field({ fieldType: 'Text' })} value="" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Not set');
  });

  it('RichText: renders the stored markdown as formatted content', () => {
    render(<ReadField field={field({ fieldType: 'RichText' })} value={'**Strengths**\n\n- Oral vocabulary'} />);
    const container = screen.getByTestId('read-field-f-1');
    expect(container.querySelector('strong')).toHaveTextContent('Strengths');
    expect(container.querySelector('li')).toHaveTextContent('Oral vocabulary');
  });

  it('RichText: shows "Not set" when blank', () => {
    render(<ReadField field={field({ fieldType: 'RichText' })} value="" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Not set');
  });

  it('Date: formats an ISO date for display', () => {
    render(<ReadField field={field({ fieldType: 'Date' })} value="2026-10-02" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Oct 2, 2026');
  });

  it('Select: resolves the option label for the stored value', () => {
    const configJson = JSON.stringify({ options: [{ value: 'sld', label: 'Specific learning disability' }] });
    render(<ReadField field={field({ fieldType: 'Select', configJson })} value="sld" />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Specific learning disability');
  });

  it('Checkbox: shows Yes/No (never "Not set" — a boolean is never blank)', () => {
    const { rerender } = render(<ReadField field={field({ fieldType: 'Checkbox', label: 'Attended' })} value={true} />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Attended — Yes');

    rerender(<ReadField field={field({ fieldType: 'Checkbox', label: 'Attended' })} value={false} />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('Attended — No');
  });

  it('Table: renders a header per column and one row per entry', () => {
    const configJson = JSON.stringify({
      columns: [
        { columnKey: 'c1', type: 'Text', label: 'Goal', required: false },
        { columnKey: 'c2', type: 'Checkbox', label: 'Mastered', required: false },
      ],
    });
    render(
      <ReadField
        field={field({ fieldType: 'Table', configJson })}
        value={[{ c1: 'Read 90 wpm', c2: true }]}
      />
    );
    const container = screen.getByTestId('read-field-f-1');
    expect(container).toHaveTextContent('Goal');
    expect(container).toHaveTextContent('Mastered');
    expect(container).toHaveTextContent('Read 90 wpm');
    expect(container).toHaveTextContent('Yes');
  });

  it('Table: shows "No rows yet." when the array is empty', () => {
    const configJson = JSON.stringify({ columns: [{ columnKey: 'c1', type: 'Text', label: 'Goal', required: false }] });
    render(<ReadField field={field({ fieldType: 'Table', configJson })} value={[]} />);
    expect(screen.getByTestId('read-field-f-1')).toHaveTextContent('No rows yet.');
  });
});
