import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
// `admin` is a staff-only namespace (plan phase 6) — see
// `docs/i18n/README.md`'s "Staff and admin namespaces".
import '@/app/lazy-routes/staff-locales';
import type { DocumentTemplateDto } from './types';

const api = vi.hoisted(() => ({
  listTemplates: vi.fn(),
  createTemplate: vi.fn(),
}));
vi.mock('./admin-templates-api', () => api);

const showToast = vi.hoisted(() => vi.fn());
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: showToast }) }));

import { TemplateListPage } from './template-list-page';

function template(): DocumentTemplateDto {
  return {
    id: 1,
    stateCode: 'OH',
    documentTypeId: 1,
    documentTypeKey: 'iep',
    documentTypeDisplayName: 'IEP',
    name: 'Ohio IEP',
    createdAt: '2026-09-15T00:00:00.000Z',
    latestVersion: { id: 1, versionNumber: 1, status: 'Published', publishedAt: '2026-09-16T00:00:00.000Z' },
  };
}

describe('TemplateListPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading, primary controls and status badge in Spanish', async () => {
    api.listTemplates.mockResolvedValue({ success: true, data: [template()] });

    await renderInSpanish(<TemplateListPage />, {
      ns: 'admin',
      wrapper: ({ children }) => <MemoryRouter>{children}</MemoryRouter>,
    });

    expect(await screen.findByRole('heading', { name: 'Plantillas de documentos' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Crear plantilla' })).toBeInTheDocument();
    expect(await screen.findByText('Publicada')).toBeInTheDocument();
  });
});
