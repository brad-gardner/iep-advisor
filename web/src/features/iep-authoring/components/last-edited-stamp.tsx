import { relativeTime } from '../lib/relative-time';

interface LastEditedStampProps {
  lastEditedAt: string | null;
  lastEditedByUserId: number | null;
  currentUserId: number;
}

// Renders "Edited {relative} by you / by another educator". Nothing when never edited.
export function LastEditedStamp({
  lastEditedAt,
  lastEditedByUserId,
  currentUserId,
}: LastEditedStampProps) {
  if (!lastEditedAt) return null;
  const who = lastEditedByUserId === currentUserId ? 'by you' : 'by another educator';
  return (
    <p className="text-xs text-brand-slate-400">
      Edited {relativeTime(lastEditedAt)} {who}
    </p>
  );
}
