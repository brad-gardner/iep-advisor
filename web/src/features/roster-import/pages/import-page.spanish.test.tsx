import '@/app/lazy-routes/staff-locales';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';
import type { ImportBatch } from '../types';

const useEducatorProfileMock = vi.fn();
vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

const api = vi.hoisted(() => ({
  downloadImportTemplate: vi.fn(),
  previewImport: vi.fn(),
  commitImport: vi.fn(),
  getImportBatches: vi.fn(),
  getImportBatch: vi.fn(),
  downloadImportErrors: vi.fn(),
}));
vi.mock('../api/import-api', () => api);

function makeProfile(overrides: Partial<EducatorProfile> = {}): EducatorProfile {
  return {
    staffProfileId: 1,
    userId: 1,
    orgRoleId: ORG_ROLE.DistrictAdmin,
    orgRoleName: 'DistrictAdmin',
    districtId: 1,
    districtName: 'Test District',
    schoolId: null,
    schoolName: null,
    isActive: true,
    stateCode: 'OH',
    title: null,
    credentials: null,
    ...overrides,
  };
}

function makeBatch(overrides: Partial<ImportBatch> = {}): ImportBatch {
  return {
    batchId: 41,
    kind: 'Students',
    fileName: 'earlier.xlsx',
    status: 'Committed',
    counts: { total: 12, new: 12, updated: 0, unchanged: 0, error: 0 },
    createdAt: '2026-09-01T10:00:00Z',
    committedAt: '2026-09-01T10:05:00Z',
    createdByName: 'Dana Admin',
    ...overrides,
  };
}

import { ImportPage } from './import-page';

describe('ImportPage in Spanish', () => {
  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset());
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile(), isLoading: false });
    api.getImportBatches.mockResolvedValue({ success: true, data: [makeBatch()] });
  });

  afterEach(() => resetTestLanguage());

  it('renders the template step, the wizard step labels and the history table in Spanish', async () => {
    await renderInSpanish(
      <MemoryRouter initialEntries={['/educator/admin/imports']}>
        <ToastProvider>
          <ImportPage />
        </ToastProvider>
      </MemoryRouter>,
      { ns: 'roster-import' }
    );

    expect(screen.getByRole('heading', { name: 'Importar', level: 1 })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Descargar la plantilla' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continuar a subir archivo' })).toBeInTheDocument();
    // The `<Trans>`-rendered intro sentence: translated prose around the
    // workbook's own (untranslated, file-format) sheet names.
    expect(screen.getByTestId('import-template-step')).toHaveTextContent(
      'Complete la hoja Students — una fila por estudiante. La hoja Values enumera las entradas permitidas, incluidas las escuelas de su distrito.'
    );
    // `WIZARD_STEP_LABEL_KEYS` feed `ProgressDots`, which folds the active
    // step's label into its own `aria-label`.
    expect(screen.getByRole('progressbar', { name: 'Paso 1 de 5: Plantilla' })).toBeInTheDocument();

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Importaciones anteriores' })).toBeInTheDocument());
    expect(screen.getByTestId('import-history-list')).toHaveTextContent('Confirmada');
  });
});
