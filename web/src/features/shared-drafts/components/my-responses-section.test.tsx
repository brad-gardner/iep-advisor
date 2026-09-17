import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { DraftResponseDto } from '../types';
import { MyResponsesSection } from './my-responses-section';

function makeResponse(overrides: Partial<DraftResponseDto> = {}): DraftResponseDto {
  return {
    id: 1,
    revisionId: 1,
    parentUserId: 1,
    parentName: 'Jamie Parent',
    targetFieldKey: null,
    targetRowId: null,
    targetLabel: null,
    kind: 'Agree',
    text: 'This works for us.',
    createdAt: '2026-09-10T00:00:00.000Z',
    status: 'Open',
    staffReply: null,
    resolvedInDraft: false,
    resolvedByName: null,
    resolvedAt: null,
    ...overrides,
  };
}

describe('MyResponsesSection', () => {
  it('renders nothing when the parent has not responded', () => {
    const { container } = render(<MyResponsesSection responses={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders stored markdown in the response text and staff reply as formatted HTML', () => {
    render(
      <MyResponsesSection
        responses={[
          makeResponse({
            status: 'Resolved',
            text: 'This **really** works for us.',
            staffReply: "Thanks, we've *updated* the goal.",
            resolvedByName: 'Case Manager',
          }),
        ]}
      />
    );

    const text = screen.getByTestId('my-response-1-text');
    expect(text.querySelector('strong')).toHaveTextContent('really');
    expect(text).toHaveTextContent('This really works for us.');

    const reply = screen.getByTestId('my-response-1-staff-reply');
    expect(reply.querySelector('em')).toHaveTextContent('updated');
  });
});
