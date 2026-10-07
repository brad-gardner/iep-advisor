import { afterEach, describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { SharedBadge } from './shared-badge';

describe('SharedBadge in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('translates the "Shared · <role>" prefix and the role label', async () => {
    await renderInSpanish(<SharedBadge role="collaborator" />);

    expect(screen.getByText('Compartido · Colaborador')).toBeInTheDocument();
  });
});
