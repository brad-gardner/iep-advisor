import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import type { EtrDocumentListItem } from '../types';
import { EtrListRow } from './etr-list-row';

interface EtrListGroupProps {
  childId: number;
  childFirstName: string;
  childLastName: string;
  etrs: EtrDocumentListItem[];
}

export function EtrListGroup({
  childId,
  childFirstName,
  childLastName,
  etrs,
}: EtrListGroupProps) {
  return (
    <Card className="p-4" data-testid="etr-list-group">
      <div className="flex items-center justify-between mb-3">
        <h3 className="font-serif text-brand-slate-800">
          {childFirstName} {childLastName}
        </h3>
        <Link
          to={`/children/${childId}`}
          className="text-[12px] font-medium text-brand-teal-500 hover:text-brand-teal-600 transition-colors"
          data-testid="etr-group-profile-link"
        >
          View profile
        </Link>
      </div>
      <div className="space-y-1">
        {etrs.map((etr) => (
          <EtrListRow key={etr.id} etr={etr} />
        ))}
      </div>
    </Card>
  );
}
