import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { NextMeetingCard } from './next-meeting-card';
import type { HomeMeetingDto } from '../types';

function makeMeeting(overrides: Partial<HomeMeetingDto> = {}): HomeMeetingDto {
  return {
    id: 1,
    title: 'Annual review',
    type: 'AnnualReview',
    startsAtUtc: new Date(2026, 8, 21, 12, 0).toISOString(),
    timeZoneId: 'America/New_York',
    durationMinutes: 60,
    studentId: 10,
    studentName: 'Ada Lovelace',
    myInviteStatus: null,
    status: 'Scheduled',
    ...overrides,
  };
}

describe('NextMeetingCard countdown in Spanish', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 8, 16, 12, 0));
  });

  afterEach(async () => {
    vi.useRealTimers();
    await resetTestLanguage();
  });

  it('translates the heading and the "In N days" countdown', async () => {
    await renderInSpanish(
      <NextMeetingCard
        meeting={makeMeeting({ startsAtUtc: new Date(2026, 8, 21, 12, 0).toISOString() })}
        showCountdown
      />
    );

    expect(screen.getByRole('heading', { name: 'Próxima reunión' })).toBeInTheDocument();
    expect(screen.getByText('En 5 días')).toBeInTheDocument();
  });

  it('translates "Today" and "Tomorrow"', async () => {
    await renderInSpanish(
      <NextMeetingCard
        meeting={makeMeeting({ startsAtUtc: new Date(2026, 8, 16, 18, 0).toISOString() })}
        showCountdown
      />
    );

    expect(screen.getByText('Hoy')).toBeInTheDocument();
  });
});
