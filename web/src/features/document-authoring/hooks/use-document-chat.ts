import { useCallback, useRef, useState } from 'react';
import { chat } from '../api/document-assist-api';
import type { ChatMessage } from '../api/assist-types';
import { friendlyAssistError } from '../lib/assist-errors';

/** A server-provided (or already-translated `friendlyAssistError`) message is
 *  resolved text; the generic case is translated at render time by the caller. */
export type UseDocumentChatError = { kind: 'server'; message: string } | { kind: 'generic' };

export interface UseDocumentChatResult {
  messages: ChatMessage[];
  isSending: boolean;
  // A transient error line shown beneath the thread (not added to messages).
  error: UseDocumentChatError | null;
  send: (text: string) => void;
}

// Holds an ephemeral, client-only chat thread scoped to one document. Nothing
// is persisted or polled. The hook is owned by the editor (not the panel) so
// the thread survives opening/closing the assistant.
export function useDocumentChat(instanceId: number): UseDocumentChatResult {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<UseDocumentChatError | null>(null);

  // The ref is written synchronously with every change (never via an effect),
  // so a send issued right after a reply lands always sees the full thread.
  const messagesRef = useRef(messages);
  const commitMessages = useCallback((next: ChatMessage[]) => {
    messagesRef.current = next;
    setMessages(next);
  }, []);
  const sendingRef = useRef(false);

  const send = useCallback(
    (text: string) => {
      const trimmed = text.trim();
      if (!trimmed || sendingRef.current) return;

      const userMessage: ChatMessage = { role: 'user', content: trimmed };
      const thread = [...messagesRef.current, userMessage];
      commitMessages(thread);
      setError(null);
      sendingRef.current = true;
      setIsSending(true);

      chat(instanceId, thread)
        .then((res) => {
          if (res.success && res.data) {
            const { reply } = res.data;
            commitMessages([...messagesRef.current, { role: 'assistant', content: reply }]);
          } else {
            setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
          }
        })
        .catch((err: unknown) => {
          // On error we append nothing — the user's message stays so they can retry.
          // `friendlyAssistError` already returns resolved (translated) text.
          setError({ kind: 'server', message: friendlyAssistError(err) });
        })
        .finally(() => {
          sendingRef.current = false;
          setIsSending(false);
        });
    },
    [instanceId, commitMessages]
  );

  return { messages, isSending, error, send };
}
