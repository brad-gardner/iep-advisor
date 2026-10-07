import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
// `admin` is a staff-only namespace (plan phase 6) — see
// `docs/i18n/README.md`'s "Staff and admin namespaces".
import '@/app/lazy-routes/staff-locales';
import type { DocumentTemplateDto, TemplateVersionDetailDto } from './types';

const api = vi.hoisted(() => ({
  listTemplates: vi.fn(),
  getTemplateVersion: vi.fn(),
  createSection: vi.fn(),
  updateSection: vi.fn(),
  deleteSection: vi.fn(),
  reorderSections: vi.fn(),
  createField: vi.fn(),
  updateField: vi.fn(),
  deleteField: vi.fn(),
  reorderFields: vi.fn(),
  publishTemplate: vi.fn(),
  createDraft: vi.fn(),
  listDocumentTypes: vi.fn(),
}));
vi.mock('./admin-templates-api', () => api);

const showToast = vi.hoisted(() => vi.fn());
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: showToast }) }));

import { TemplateBuilderPage } from './template-builder-page';

function template(): DocumentTemplateDto {
  return {
    id: 1,
    stateCode: 'OH',
    documentTypeId: 1,
    documentTypeKey: 'iep',
    documentTypeDisplayName: 'IEP',
    name: 'Ohio IEP',
    createdAt: '2026-09-15T00:00:00.000Z',
    latestVersion: { id: 1, versionNumber: 1, status: 'Draft', publishedAt: null },
  };
}

function version(): TemplateVersionDetailDto {
  return {
    id: 1,
    documentTemplateId: 1,
    versionNumber: 1,
    status: 'Draft',
    publishedAt: null,
    rowVersion: 'abc',
    sections: [],
  };
}

describe('TemplateBuilderPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the structure heading and add-section control in Spanish', async () => {
    api.listTemplates.mockResolvedValue({ success: true, data: [template()] });
    api.getTemplateVersion.mockResolvedValue({ success: true, data: version() });

    await renderInSpanish(<TemplateBuilderPage />, {
      ns: 'admin',
      wrapper: ({ children }) => (
        <MemoryRouter initialEntries={['/admin/templates/1']}>
          <Routes>
            <Route path="/admin/templates/:templateId" element={children} />
          </Routes>
        </MemoryRouter>
      ),
    });

    expect(await screen.findByText('Estructura')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Agregar sección/ })).toBeInTheDocument();
    expect(screen.getByText('Vista previa del formulario')).toBeInTheDocument();
  });
});
