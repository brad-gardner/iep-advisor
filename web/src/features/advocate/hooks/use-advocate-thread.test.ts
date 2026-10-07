import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { StreamAdvocateMessageHandlers } from '../api/advocate-api';
import type { AdvocateThreadDetailDto } from '../types/advocate';

const api = vi.hoisted(() => ({
  getAdvocateThread: vi.fn(),
  streamAdvocateMessage: vi.fn(),
}));
vi.mock('../api/advocate-api', async () => {
  const actual = await vi.importActual<typeof import('../api/advocate-api')>('../api/advocate-api');
  return { ...actual, ...api };
});

import { useAdvocateThread } from './use-advocate-thread';

function detail(id: number): AdvocateThreadDetailDto {
  return {
    id,
    childProfileId: 4,
    title: 'PWN question',
    createdAt: '2026-01-01T00:00:00.000Z',
    updatedAt: '2026-01-01T00:00:00.000Z',
    lastMessageAt: '2026-01-01T00:00:00.000Z',
    messages: [],
    disclaimer: 'Not legal advice.',
  };
}

describe('useAdvocateThread — synthetic fallback message', () => {
  beforeEach(() => {
    api.getAdvocateThread.mockReset();
    api.streamAdvocateMessage.mockReset();
  });

  it("carries the done frame's generatedLanguage onto the locally-appended assistant message when the post-done refetch fails", async () => {
    api.getAdvocateThread.mockResolvedValueOnce({ success: true, data: detail(1) });
    api.getAdvocateThread.mockRejectedValueOnce(new Error('network'));

    let handlers: StreamAdvocateMessageHandlers | null = null;
    api.streamAdvocateMessage.mockImplementation(
      (_id: number, _body: unknown, h: StreamAdvocateMessageHandlers) => {
        handlers = h;
        return new Promise<void>(() => {}); // the test drives completion via `handlers` directly
      },
    );

    const { result } = renderHook(() => useAdvocateThread(1));
    await waitFor(() => expect(result.current.loading).toBe(false));

    act(() => {
      result.current.send('What is PWN?');
    });
    await waitFor(() => expect(handlers).not.toBeNull());

    await act(async () => {
      handlers!.onDone({
        messageId: 99,
        contentMarkdown: 'Here is the answer.',
        citations: [],
        suggestions: [],
        truncated: false,
        disclaimer: 'Not legal advice.',
        generatedLanguage: 'es',
      });
      // Let the fire-and-forget `settleFromServer` (rejected refetch, then the
      // local-append fallback) run to completion.
      await Promise.resolve();
      await Promise.resolve();
      await Promise.resolve();
    });

    await waitFor(() => expect(result.current.messages.some((m) => m.id === 99)).toBe(true));
    const assistantMessage = result.current.messages.find((m) => m.id === 99);
    expect(assistantMessage?.generatedLanguage).toBe('es');
  });
});
