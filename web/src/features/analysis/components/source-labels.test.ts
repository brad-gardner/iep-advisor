import { afterEach, describe, expect, it } from 'vitest';
import i18n from '@/lib/i18n';
import type { IepDocument } from '@/types/api';
import type { EtrDocument } from '@/features/etr-documents/types';
import type { ProgressReport } from '@/features/progress-reports/types';
import { etrLabel, iepLabel, progressReportLabel } from './source-labels';

function makeIep(overrides: Partial<IepDocument> = {}): IepDocument {
  return {
    id: 1,
    childProfileId: 1,
    fileName: 'iep.pdf',
    uploadDate: '2026-01-10',
    iepDate: '2026-01-15',
    status: 'parsed',
    fileSizeBytes: 100,
    meetingType: null,
    attendees: null,
    notes: null,
    createdAt: '2026-01-10T00:00:00.000Z',
    ...overrides,
  };
}

function makeEtr(overrides: Partial<EtrDocument> = {}): EtrDocument {
  return {
    id: 1,
    childProfileId: 1,
    fileName: 'etr.pdf',
    uploadDate: '2026-02-10',
    evaluationDate: '2026-02-15',
    evaluationType: null,
    documentState: 'ohio',
    notes: null,
    status: 'parsed',
    fileSizeBytes: 100,
    createdAt: '2026-02-10T00:00:00.000Z',
    ...overrides,
  };
}

function makeProgressReport(overrides: Partial<ProgressReport> = {}): ProgressReport {
  return {
    id: 1,
    iepDocumentId: 1,
    childProfileId: 1,
    fileName: 'progress.pdf',
    uploadDate: '2026-03-01',
    reportingPeriodStart: '2026-01-01',
    reportingPeriodEnd: '2026-03-01',
    notes: null,
    status: 'parsed',
    errorMessage: null,
    fileSizeBytes: 100,
    createdAt: '2026-03-01T00:00:00.000Z',
    ...overrides,
  };
}

describe('source-labels', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('labels an IEP by its iepDate', () => {
    expect(iepLabel(makeIep())).toBe('IEP Jan 15, 2026');
  });

  it('falls back to the upload date when an IEP has no iepDate', () => {
    expect(iepLabel(makeIep({ iepDate: null, uploadDate: '2026-01-10' }))).toBe('IEP Jan 10, 2026');
  });

  it('labels an ETR by its evaluationDate', () => {
    expect(etrLabel(makeEtr())).toBe('ETR Feb 15, 2026');
  });

  it('shows the reporting period range when both dates are set', () => {
    const label = progressReportLabel(
      makeProgressReport({ reportingPeriodStart: '2026-01-01', reportingPeriodEnd: '2026-03-01' })
    );
    expect(label).toBe('Progress Report Jan 1, 2026 – Mar 1, 2026');
  });

  it('shows a single date when only the start of the period is set', () => {
    const label = progressReportLabel(
      makeProgressReport({ reportingPeriodStart: '2026-01-01', reportingPeriodEnd: null })
    );
    expect(label).toBe('Progress Report Jan 1, 2026');
  });

  it('falls back to the upload date when no reporting period is set at all', () => {
    const label = progressReportLabel(
      makeProgressReport({ reportingPeriodStart: null, reportingPeriodEnd: null, uploadDate: '2026-03-05' })
    );
    expect(label).toBe('Progress Report Mar 5, 2026');
  });
});
