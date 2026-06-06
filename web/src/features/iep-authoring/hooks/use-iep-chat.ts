import { useCallback, useEffect, useRef, useState } from 'react';
import { chat } from '../api/iep-assist-api';
import type { ChatMessage } from '../api/iep-assist-types';
import { friendlyAssistError } from '../lib/assist-errors';

export interface UseIepChatResult {
  messages: ChatMessage[];
  isSending: boolean;
  // A transient error line shown beneath the thread (not added to messages).
  error: string | null;
  send: (text: string) => void;
}

// Holds an ephemeral, client-only chat thread scoped to one draft. Nothing is
// persisted or polled: messages live only for the lifetime of the panel.
export function useIepChat(draftId: number): UseIepChatResult {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Mirror messages + in-flight in refs so `send` reads the latest thread without
  // depending on them — avoids a stale-closure thread if send fires rapidly.
  const messagesRef = useRef(messages);
  useEffect(() => {
    messagesRef.current = messages;
  }, [messages]);
  const sendingRef = useRef(false);

  const send = useCallback(
    (text: string) => {
      const trimmed = text.trim();
      if (!trimmed || sendingRef.current) return;

      const userMessage: ChatMessage = { role: 'user', content: trimmed };
      const thread = [...messagesRef.current, userMessage];
      setMessages(thread);
      setError(null);
      sendingRef.current = true;
      setIsSending(true);

      chat(draftId, thread)
        .then((res) => {
          if (res.success && res.data) {
            const { reply } = res.data;
            setMessages((prev) => [...prev, { role: 'assistant', content: reply }]);
          } else {
            setError(res.message || 'The assistant could not respond. Please try again.');
          }
        })
        .catch((err: unknown) => {
          // On error we append nothing — the user's message stays so they can retry.
          setError(friendlyAssistError(err));
        })
        .finally(() => {
          sendingRef.current = false;
          setIsSending(false);
        });
    },
    [draftId]
  );

  return { messages, isSending, error, send };
}
