import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';
import type { ImportBatch, ImportPreview, ImportResult } from '../types';

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

import { ImportPage } from './import-page';

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

function makePreview(overrides: Partial<ImportPreview> = {}): ImportPreview {
  return {
    batchId: 42,
    kind: 'Students',
    fileName: 'roster.xlsx',
    status: 'Previewed',
    counts: { total: 3, new: 2, updated: 1, unchanged: 0, error: 0 },
    rows: [
      { rowNumber: 2, outcome: 'New', key: '000123', displayName: 'Ada Lovelace', message: null, changes: [] },
      { rowNumber: 3, outcome: 'New', key: '000124', displayName: 'Alan Turing', message: null, changes: [] },
      {
        rowNumber: 4,
        outcome: 'Updated',
        key: '000125',
        displayName: 'Grace Hopper',
        message: null,
        changes: ['Grade: 6 → 7'],
      },
    ],
    createdAt: '2026-09-15T10:00:00Z',
    committedAt: null,
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

function xlsxFile(name = 'roster.xlsx', size = 1024): File {
  const file = new File(['x'], name, {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

function renderPage(initialEntry = '/educator/admin/imports') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <ToastProvider>
        <ImportPage />
      </ToastProvider>
    </MemoryRouter>
  );
}

describe('ImportPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile(), isLoading: false });
    api.getImportBatches.mockResolvedValue({ success: true, data: [makeBatch()] });
    api.downloadImportTemplate.mockResolvedValue({
      blob: new Blob(['t']),
      fileName: 'students-import-template.xlsx',
    });
    api.downloadImportErrors.mockResolvedValue({ blob: new Blob(['e']), fileName: 'import-errors.xlsx' });
    // jsdom has no object-URL support; the download helper needs both.
    URL.createObjectURL = vi.fn(() => 'blob:mock');
    URL.revokeObjectURL = vi.fn();
  });

  it('walks template → upload → preview → commit → result and reloads history', async () => {
    const user = userEvent.setup();
    api.previewImport.mockResolvedValue({ success: true, data: makePreview() });
    const result: ImportResult = {
      batchId: 42,
      committed: { new: 2, updated: 1, unchanged: 0 },
      skipped: 0,
      status: 'Committed',
    };
    api.commitImport.mockResolvedValue({ success: true, data: result });

    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Import' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Students' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '1');

    // History loaded once on mount.
    await waitFor(() => expect(screen.getByText('earlier.xlsx')).toBeInTheDocument());
    expect(api.getImportBatches).toHaveBeenCalledTimes(1);

    // Template step.
    await user.click(screen.getByTestId('import-template-download'));
    await waitFor(() => expect(api.downloadImportTemplate).toHaveBeenCalledWith('Students'));
    expect(URL.createObjectURL).toHaveBeenCalled();
    await user.click(screen.getByTestId('import-template-continue'));

    // Upload step.
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '2');
    const input = screen.getByLabelText('Workbook (.xlsx)');
    await user.upload(input, xlsxFile());
    expect(screen.getByTestId('import-file-summary')).toHaveTextContent('roster.xlsx');
    await user.click(screen.getByTestId('import-upload-preview'));
    await waitFor(() => expect(api.previewImport).toHaveBeenCalledTimes(1));
    expect(api.previewImport.mock.calls[0][0]).toBe('Students');
    expect((api.previewImport.mock.calls[0][1] as File).name).toBe('roster.xlsx');

    // Preview step.
    await screen.findByTestId('import-preview-step');
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '3');
    expect(screen.getByTestId('import-counts-new')).toHaveTextContent('2 new');
    expect(screen.getByTestId('import-counts-updated')).toHaveTextContent('1 updated');
    expect(screen.getByTestId('import-counts-error')).toHaveTextContent('0 errors');
    const rows = within(screen.getByTestId('import-preview-rows'));
    expect(rows.getByText('Ada Lovelace')).toBeInTheDocument();
    expect(rows.getByText('Grade: 6 → 7')).toBeInTheDocument();
    expect(screen.queryByTestId('import-download-errors')).not.toBeInTheDocument();

    // Commit via the confirm dialog.
    const commitButton = screen.getByTestId('import-preview-commit');
    expect(commitButton).toHaveTextContent('Import 3 rows');
    await user.click(commitButton);
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '4');
    await user.click(screen.getByTestId('import-commit-dialog-confirm'));
    await waitFor(() => expect(api.commitImport).toHaveBeenCalledWith(42, { commitValid: false }));

    // Result step + history reload.
    await screen.findByTestId('import-result-step');
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '5');
    expect(screen.getByTestId('import-result-notice')).toHaveTextContent('2 new, 1 updated, 0 unchanged');
    await waitFor(() => expect(api.getImportBatches).toHaveBeenCalledTimes(2));
  });

  it('offers the error report when the preview has error rows and imports valid rows only', async () => {
    const user = userEvent.setup();
    api.previewImport.mockResolvedValue({
      success: true,
      data: makePreview({
        counts: { total: 3, new: 1, updated: 1, unchanged: 0, error: 1 },
        rows: [
          { rowNumber: 2, outcome: 'New', key: '000123', displayName: 'Ada Lovelace', message: null, changes: [] },
          { rowNumber: 3, outcome: 'Error', key: '000124', displayName: 'Alan Turing', message: 'Unknown school', changes: [] },
          { rowNumber: 4, outcome: 'Updated', key: '000125', displayName: 'Grace Hopper', message: null, changes: [] },
        ],
      }),
    });
    api.commitImport.mockResolvedValue({
      success: true,
      data: { batchId: 42, committed: { new: 1, updated: 1, unchanged: 0 }, skipped: 1, status: 'Committed' },
    });

    renderPage();
    await user.click(screen.getByTestId('import-template-continue'));
    await user.upload(screen.getByLabelText('Workbook (.xlsx)'), xlsxFile());
    await user.click(screen.getByTestId('import-upload-preview'));
    await screen.findByTestId('import-preview-step');

    expect(screen.getByTestId('import-counts-error')).toHaveTextContent('1 error');
    expect(screen.getByText('Unknown school')).toBeInTheDocument();

    await user.click(screen.getByTestId('import-download-errors'));
    await waitFor(() => expect(api.downloadImportErrors).toHaveBeenCalledWith(42));
    expect(URL.createObjectURL).toHaveBeenCalled();

    expect(screen.getByTestId('import-preview-commit')).toHaveTextContent('Import 2 valid rows only');
    await user.click(screen.getByTestId('import-preview-commit'));
    await user.click(screen.getByTestId('import-commit-dialog-confirm'));
    await waitFor(() => expect(api.commitImport).toHaveBeenCalledWith(42, { commitValid: true }));
    expect(await screen.findByTestId('import-result-notice')).toHaveTextContent('1 skipped');
  });

  it('keeps a refused commit inside the dialog', async () => {
    const user = userEvent.setup();
    api.previewImport.mockResolvedValue({ success: true, data: makePreview() });
    api.commitImport.mockResolvedValue({
      success: false,
      message: 'Fix the errors or choose to import valid rows only.',
    });

    renderPage();
    await user.click(screen.getByTestId('import-template-continue'));
    await user.upload(screen.getByLabelText('Workbook (.xlsx)'), xlsxFile());
    await user.click(screen.getByTestId('import-upload-preview'));
    await screen.findByTestId('import-preview-step');
    await user.click(screen.getByTestId('import-preview-commit'));
    await user.click(screen.getByTestId('import-commit-dialog-confirm'));

    expect(
      await screen.findByText('Fix the errors or choose to import valid rows only.')
    ).toBeInTheDocument();
    expect(screen.queryByTestId('import-result-step')).not.toBeInTheDocument();
  });

  it('rejects a file over 5 MB client-side without uploading', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByTestId('import-template-continue'));

    await user.upload(screen.getByLabelText('Workbook (.xlsx)'), xlsxFile('big.xlsx', 6 * 1024 * 1024));

    expect(screen.getByTestId('import-upload-error')).toHaveTextContent('File is larger than 5 MB');
    expect(screen.getByTestId('import-upload-preview')).toBeDisabled();
    expect(api.previewImport).not.toHaveBeenCalled();
  });

  it('rejects a macro-enabled workbook client-side', async () => {
    // `accept` filtering is bypassed so the extension check itself is exercised.
    const user = userEvent.setup({ applyAccept: false });
    renderPage();
    await user.click(screen.getByTestId('import-template-continue'));

    const input = screen.getByLabelText('Workbook (.xlsx)');
    await user.upload(input, xlsxFile('macros.xlsm'));

    expect(screen.getByTestId('import-upload-error')).toHaveTextContent('Only .xlsx workbooks are accepted');
    expect(api.previewImport).not.toHaveBeenCalled();
  });

  it('switches to the Staff sheet from ?kind=Staff and resets the wizard on toggle', async () => {
    const user = userEvent.setup();
    renderPage('/educator/admin/imports?kind=Staff');

    expect(screen.getByRole('radio', { name: 'Staff' })).toHaveAttribute('aria-checked', 'true');
    await user.click(screen.getByTestId('import-template-download'));
    await waitFor(() => expect(api.downloadImportTemplate).toHaveBeenCalledWith('Staff'));

    await user.click(screen.getByTestId('import-template-continue'));
    expect(screen.getByTestId('import-upload-step')).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Students' }));
    expect(screen.getByTestId('import-template-step')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Students' })).toHaveAttribute('aria-checked', 'true');
  });

  it('shows a not-available state to non-admin staff', () => {
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile({ orgRoleId: ORG_ROLE.Teacher, orgRoleName: 'Teacher' }),
      isLoading: false,
    });
    renderPage();

    expect(screen.getByTestId('roster-import-not-available')).toBeInTheDocument();
    expect(screen.queryByTestId('import-template-step')).not.toBeInTheDocument();
    expect(api.getImportBatches).not.toHaveBeenCalled();
  });
});
