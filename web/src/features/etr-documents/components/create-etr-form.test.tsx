import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const api = vi.hoisted(() => ({ create: vi.fn() }));
vi.mock('../api/etr-documents-api', async (orig) => ({
  ...(await orig<typeof import('../api/etr-documents-api')>()),
  ...api,
}));

import { CreateEtrForm } from './create-etr-form';

describe('CreateEtrForm', () => {
  beforeEach(() => {
    api.create.mockReset();
    api.create.mockResolvedValue({ success: true, data: {} });
  });

  it('submits notes as the markdown the rich text editor emits', async () => {
    const onCreated = vi.fn();
    render(<CreateEtrForm childId={7} onCreated={onCreated} onCancel={() => {}} />);

    fireEvent.change(screen.getByTestId('etr-evaluation-date'), { target: { value: '2026-09-01' } });
    fireEvent.change(screen.getByTestId('etr-evaluation-type'), { target: { value: 'initial' } });
    fireEvent.change(screen.getByTestId('etr-notes'), { target: { value: 'Findings: **eligible**.' } });

    fireEvent.click(screen.getByTestId('etr-create-submit'));

    await waitFor(() =>
      expect(api.create).toHaveBeenCalledWith(7, {
        evaluationDate: '2026-09-01',
        evaluationType: 'initial',
        documentState: 'draft',
        notes: 'Findings: **eligible**.',
      })
    );
    await waitFor(() => expect(onCreated).toHaveBeenCalled());
  });
});
