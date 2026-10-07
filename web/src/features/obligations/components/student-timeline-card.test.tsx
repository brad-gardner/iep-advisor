import { afterEach, describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { apiRejection } from '@/test/axios-rejection';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { ObligationDto } from '../types';

const obligationsApi = vi.hoisted(() => ({ listStudentObligations: vi.fn() }));
vi.mock('../api/obligations-api', () => obligationsApi);

import { StudentTimelineCard } from './student-timeline-card';
// `obligations` is a staff-only namespace (plan phase 5) — its English
// isn't bundled in `resources` (see `lib/i18n/index.ts`), only registered
// by this side-effect import, exactly as the real lazy route chunk
// (`app/lazy-routes/staff-routes.tsx`) registers it before the page that
// hosts this card (the educator student detail page) can render.
import '../staff-locales';

const obligations: ObligationDto[] = [
  {
    kind: 'AnnualReview',
    dueDate: '2026-10-01',
    status: 'DueSoon',
    sourceLabel: 'from IEP date',
    ownerUserId: 7,
    ownerName: 'Casey Manager',
    schoolStudentId: 10,
    studentName: 'Ada Lovelace',
    daysUntilDue: 16,
    ruleProfile: 'OH',
  },
  {
    kind: 'Reevaluation',
    dueDate: '2024-01-01',
    status: 'Overdue',
    sourceLabel: 'from ETR date',
    ownerUserId: 7,
    ownerName: 'Casey Manager',
    schoolStudentId: 10,
    studentName: 'Ada Lovelace',
    daysUntilDue: -600,
    ruleProfile: 'OH',
  },
  {
    kind: 'EtrDue',
    dueDate: null,
    status: 'Unknown',
    sourceLabel: 'no ETR on file',
    ownerUserId: null,
    ownerName: null,
    schoolStudentId: 10,
    studentName: 'Ada Lovelace',
    daysUntilDue: null,
    ruleProfile: 'OH',
  },
];

describe('StudentTimelineCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => resetTestLanguage());

  it('renders a status chip (icon + text) per obligation kind', async () => {
    obligationsApi.listStudentObligations.mockResolvedValue({ success: true, data: obligations });
    render(<StudentTimelineCard studentId={10} onEditDates={vi.fn()} />);

    expect(await screen.findByTestId('timeline-row-AnnualReview')).toHaveTextContent('Due soon');
    expect(screen.getByTestId('timeline-row-Reevaluation')).toHaveTextContent('Overdue');
    expect(screen.getByTestId('timeline-row-EtrDue')).toHaveTextContent('Unknown');
    // Status is never colour-only: each chip carries an icon alongside the text.
    expect(screen.getByTestId('obligation-status-DueSoon').querySelector('svg')).toBeInTheDocument();
  });

  it('shows an Unknown chip (not a healthy default) when a date is missing', async () => {
    obligationsApi.listStudentObligations.mockResolvedValue({ success: true, data: obligations });
    render(<StudentTimelineCard studentId={10} onEditDates={vi.fn()} />);
    expect(await screen.findByTestId('timeline-row-EtrDue')).toHaveTextContent('—');
  });

  it('calls the host callback to open the edit-dates drawer', async () => {
    const user = userEvent.setup();
    obligationsApi.listStudentObligations.mockResolvedValue({ success: true, data: obligations });
    const onEditDates = vi.fn();
    render(<StudentTimelineCard studentId={10} onEditDates={onEditDates} />);
    await screen.findByTestId('student-timeline-list');

    await user.click(screen.getByTestId('timeline-edit-dates'));
    expect(onEditDates).toHaveBeenCalledTimes(1);
  });

  it('surfaces a load failure as an inline alert', async () => {
    obligationsApi.listStudentObligations.mockRejectedValue(apiRejection('Could not compute obligations'));
    render(<StudentTimelineCard studentId={10} onEditDates={vi.fn()} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not compute obligations');
  });

  it('renders the heading and status/kind labels in Spanish', async () => {
    obligationsApi.listStudentObligations.mockResolvedValue({ success: true, data: obligations });

    await renderInSpanish(<StudentTimelineCard studentId={10} onEditDates={vi.fn()} />, { ns: 'obligations' });

    expect(await screen.findByText('Cronología')).toBeInTheDocument();
    expect(screen.getByTestId('timeline-row-AnnualReview')).toHaveTextContent('Próxima a vencer');
    expect(screen.getByTestId('timeline-row-Reevaluation')).toHaveTextContent('Vencida');
    expect(screen.getByText('Editar fechas')).toBeInTheDocument();
  });
});
