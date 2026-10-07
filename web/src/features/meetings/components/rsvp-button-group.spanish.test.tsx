import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { RsvpButtonGroup } from './rsvp-button-group';

describe('RsvpButtonGroup in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('translates the Accept/Tentative/Decline buttons', async () => {
    await renderInSpanish(<RsvpButtonGroup onRespond={vi.fn()} pending={null} testIdPrefix="rsvp" />);

    expect(screen.getByTestId('rsvp-accept')).toHaveTextContent('Aceptar');
    expect(screen.getByTestId('rsvp-tentative')).toHaveTextContent('Tal vez');
    expect(screen.getByTestId('rsvp-decline')).toHaveTextContent('Rechazar');
  });
});
