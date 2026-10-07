import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
// `admin` is a staff-only namespace (plan phase 6) — see
// `docs/i18n/README.md`'s "Staff and admin namespaces".
import '@/app/lazy-routes/staff-locales';
import type { AuditIntegrityRunDto } from '../types';

const api = vi.hoisted(() => ({
  listAuditIntegrityRuns: vi.fn(),
  runAuditIntegrityCheck: vi.fn(),
}));
vi.mock('../api/audit-admin-api', () => api);

import { AdminAuditPage } from './admin-audit-page';

function run(): AuditIntegrityRunDto {
  return {
    id: 1,
    startedAt: '2026-09-16T03:00:00.000Z',
    completedAt: '2026-09-16T03:00:05.000Z',
    rowsChecked: 12000,
    firstBrokenId: null,
    status: 'Ok',
    detail: null,
  };
}

describe('AdminAuditPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading and status badge in Spanish', async () => {
    api.listAuditIntegrityRuns.mockResolvedValue({ success: true, data: [run()] });

    await renderInSpanish(<AdminAuditPage />, { ns: 'admin' });

    expect(await screen.findByRole('heading', { name: 'Integridad de la auditoría' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ejecutar verificación ahora' })).toBeInTheDocument();
    expect(await screen.findByTestId('audit-run-status-1')).toHaveTextContent('Correcto');
  });
});
