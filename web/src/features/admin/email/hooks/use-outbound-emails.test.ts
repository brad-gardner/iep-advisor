import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { OutboundEmailDto, OutboundEmailStatusFilter } from '../types';

const api = vi.hoisted(() => ({
  listOutboundEmails: vi.fn(),
  getOutboundEmailStatus: vi.fn(),
}));
vi.mock('../api/email-admin-api', () => api);

import { useOutboundEmails } from './use-outbound-emails';

function email(id: number, status: OutboundEmailDto['status']): OutboundEmailDto {
  return {
    id,
    toEmail: 'parent@example.com',
    subject: 'Meeting reminder',
    kind: 'MeetingReminder',
    status,
    attempts: 0,
    lastError: null,
    nextAttemptAt: '2026-09-16T00:00:00.000Z',
    sentAt: null,
    correlationId: null,
    createdAt: '2026-09-16T00:00:00.000Z',
  };
}

const statusOk = { success: true, data: { configured: true, queued: 0, failed: 0, sending: 0, lastSentAt: null } };

describe('useOutboundEmails', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getOutboundEmailStatus.mockResolvedValue(statusOk);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('loads the filtered list and the status banner on mount', async () => {
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [email(1, 'Failed')] });

    const { result } = renderHook(() => useOutboundEmails('Failed'));

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(api.listOutboundEmails).toHaveBeenCalledWith('Failed');
    expect(result.current.emails).toHaveLength(1);
    expect(result.current.status).toEqual(statusOk.data);
  });

  it('polls every 15s while a row is Queued/Sending, and stops once none are', async () => {
    vi.useFakeTimers();
    api.listOutboundEmails
      .mockResolvedValueOnce({ success: true, data: [email(1, 'Queued')] })
      .mockResolvedValueOnce({ success: true, data: [email(1, 'Sent')] });

    renderHook(() => useOutboundEmails('All'));

    await act(async () => {
      await Promise.resolve();
    });
    expect(api.listOutboundEmails).toHaveBeenCalledTimes(1);

    // First poll fires because the loaded row was Queued.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });
    expect(api.listOutboundEmails).toHaveBeenCalledTimes(2);

    // The second load resolved to an all-Sent snapshot, so the next tick polls no more.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });
    expect(api.listOutboundEmails).toHaveBeenCalledTimes(2);
  });

  it('re-fetches with the new filter when the status filter changes', async () => {
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [] });

    const { result, rerender } = renderHook(
      ({ status }: { status: OutboundEmailStatusFilter }) => useOutboundEmails(status),
      { initialProps: { status: 'Failed' } }
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    rerender({ status: 'Queued' });
    await waitFor(() => expect(api.listOutboundEmails).toHaveBeenLastCalledWith('Queued'));
  });

  it('keeps polling under the Failed filter while the unfiltered queue still has rows in flight', async () => {
    vi.useFakeTimers();
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [] }); // the Failed tab never shows Queued/Sending rows
    api.getOutboundEmailStatus
      .mockResolvedValueOnce({ success: true, data: { configured: true, queued: 0, failed: 0, sending: 2, lastSentAt: null } })
      .mockResolvedValue({ success: true, data: { configured: true, queued: 0, failed: 0, sending: 0, lastSentAt: null } });

    renderHook(() => useOutboundEmails('Failed'));
    await act(async () => {
      await Promise.resolve();
    });
    expect(api.listOutboundEmails).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });
    expect(api.listOutboundEmails).toHaveBeenCalledTimes(2); // sending > 0 kept the poll alive

    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });
    expect(api.listOutboundEmails).toHaveBeenCalledTimes(2); // nothing in flight any more
    vi.useRealTimers();
  });
});
