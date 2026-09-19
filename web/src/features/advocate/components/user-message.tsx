interface UserMessageProps {
  text: string;
  /** Not yet confirmed by the server (in flight, stopped, or failed). */
  pending?: boolean;
  'data-testid'?: string;
}

/** The parent's own message, right-aligned. Plain text — never markdown-rendered. */
export function UserMessage({ text, pending = false, 'data-testid': testId = 'advocate-user-message' }: UserMessageProps) {
  return (
    <li className="flex justify-end" data-testid={testId} data-pending={pending ? 'true' : undefined}>
      <p className="max-w-[85%] whitespace-pre-wrap break-words rounded-card rounded-br-sm bg-brand-teal-500 px-3.5 py-2.5 text-sm text-white">
        {text}
      </p>
    </li>
  );
}
