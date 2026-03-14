import { Link } from 'react-router-dom';
import { ArrowRightLeft } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useIepTimeline } from '../hooks/use-iep-timeline';
import type { TimelineEntry } from '@/types/api';

const MEETING_TYPE_LABELS: Record<string, string> = {
  initial: 'Initial IEP',
  annual_review: 'Annual Review',
  amendment: 'Amendment',
  reevaluation: 'Reevaluation',
};

function TimelineEntryCard({ entry }: { entry: TimelineEntry }) {
  const statusVariant = entry.status === 'parsed' ? 'success' : entry.status === 'error' ? 'error' : 'neutral';

  return (
    <Card className="relative">
      <div className="flex justify-between items-start">
        <div>
          <p className="font-serif text-[17px] font-semibold text-brand-slate-800">
            {entry.iepDate
              ? new Date(entry.iepDate).toLocaleDateString('en-US', {
                  year: 'numeric',
                  month: 'long',
                  day: 'numeric',
                })
              : 'No date'}
          </p>
          <div className="flex items-center gap-2 mt-1.5">
            {entry.meetingType && (
              <Badge variant="info">
                {MEETING_TYPE_LABELS[entry.meetingType] || entry.meetingType}
              </Badge>
            )}
            <Badge variant={statusVariant}>{entry.status}</Badge>
          </div>
        </div>
      </div>
      <div className="flex gap-4 mt-3 text-[13px] text-brand-slate-500">
        <span>
          <span className="font-medium text-brand-slate-700">{entry.goalCount}</span> goals
        </span>
        <span>
          <span className="font-medium text-brand-slate-700">{entry.sectionCount}</span> sections
        </span>
        {entry.redFlagCount > 0 && (
          <span className="text-brand-red">
            <span className="font-medium">{entry.redFlagCount}</span> red flags
          </span>
        )}
      </div>
    </Card>
  );
}

function CompareLink({
  childId,
  currentId,
  previousId,
}: {
  childId: number;
  currentId: number;
  previousId: number;
}) {
  return (
    <div className="flex justify-center py-1">
      <Link
        to={`/children/${childId}/compare/${previousId}/${currentId}`}
        className="inline-flex items-center gap-1.5 text-[12px] font-medium text-brand-teal-500 hover:text-brand-teal-600 transition-colors"
      >
        <ArrowRightLeft className="w-3.5 h-3.5" strokeWidth={1.8} />
        Compare
      </Link>
    </div>
  );
}

export function IepTimeline({ childId }: { childId: number }) {
  const { timeline, isLoading } = useIepTimeline(childId);

  if (isLoading) {
    return (
      <div className="flex justify-center py-6">
        <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!timeline || timeline.ieps.length === 0) {
    return (
      <p className="text-[13px] text-brand-slate-400 py-4">
        No IEPs found. Upload at least two IEPs to see a timeline and compare versions.
      </p>
    );
  }

  const entries = timeline.ieps;

  return (
    <div className="relative">
      {/* Vertical connecting line */}
      {entries.length > 1 && (
        <div className="absolute left-4 top-6 bottom-6 w-px bg-brand-slate-200" />
      )}

      <div className="space-y-0">
        {entries.map((entry, index) => (
          <div key={entry.id}>
            <div className="relative flex gap-4">
              {/* Timeline dot */}
              <div className="relative z-10 flex items-start pt-5">
                <div className="w-2.5 h-2.5 rounded-full bg-brand-teal-500 ring-4 ring-white shrink-0 ml-[10px]" />
              </div>

              {/* Card */}
              <div className="flex-1 pb-2">
                <TimelineEntryCard entry={entry} />
              </div>
            </div>

            {/* Compare link between adjacent entries */}
            {index < entries.length - 1 && (
              <CompareLink
                childId={childId}
                currentId={entry.id}
                previousId={entries[index + 1].id}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
