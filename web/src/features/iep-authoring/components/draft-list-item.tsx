import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { relativeTime } from '../lib/relative-time';
import type { IepDraftDto } from '../types';

interface DraftListItemProps {
  draft: IepDraftDto;
  to: string;
}

export function DraftListItem({ draft, to }: DraftListItemProps) {
  return (
    <Link to={to} data-testid={`draft-link-${draft.id}`} className="block">
      <Card className="hover:border-brand-teal-300 transition-colors">
        <div className="flex items-center justify-between gap-4">
          <div>
            <p className="font-medium text-brand-slate-800">
              {draft.title || 'Untitled IEP draft'}
            </p>
            <p className="text-xs text-brand-slate-400 mt-0.5">{draft.status}</p>
          </div>
          {draft.lastEditedAt && (
            <p className="text-xs text-brand-slate-400 shrink-0">
              Edited {relativeTime(draft.lastEditedAt)}
            </p>
          )}
        </div>
      </Card>
    </Link>
  );
}
