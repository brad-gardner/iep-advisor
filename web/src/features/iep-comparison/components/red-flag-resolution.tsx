import { CheckCircle, AlertTriangle, XCircle } from 'lucide-react';
import { Card } from '@/components/ui/card';
import type { RedFlagResolutionResult } from '@/types/api';

function FlagGroup({
  title,
  description,
  icon: Icon,
  items,
  colorClass,
  borderClass,
}: {
  title: string;
  description: string;
  icon: typeof CheckCircle;
  items: { title: string }[];
  colorClass: string;
  borderClass: string;
}) {
  if (items.length === 0) return null;

  return (
    <div className={`rounded-card border p-4 ${borderClass}`}>
      <div className="flex items-center gap-2 mb-2">
        <Icon className={`w-4.5 h-4.5 ${colorClass}`} strokeWidth={1.8} />
        <h4 className={`text-sm font-semibold ${colorClass}`}>{title}</h4>
      </div>
      <p className="text-[12px] text-brand-slate-400 mb-3">{description}</p>
      <ul className="space-y-1.5">
        {items.map((item, i) => (
          <li key={i} className="text-sm text-brand-slate-700 flex items-start gap-2">
            <span className={`mt-1.5 w-1.5 h-1.5 rounded-full shrink-0 ${colorClass.replace('text-', 'bg-')}`} />
            {item.title}
          </li>
        ))}
      </ul>
    </div>
  );
}

export function RedFlagResolution({ resolution }: { resolution: RedFlagResolutionResult }) {
  const hasAny =
    resolution.resolved.length > 0 ||
    resolution.persisting.length > 0 ||
    resolution.newFlags.length > 0;

  if (!hasAny) return null;

  return (
    <Card>
      <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800 mb-4">
        Red Flag Resolution
      </h3>

      <div className="space-y-3">
        <FlagGroup
          title="Resolved"
          description="These issues were addressed"
          icon={CheckCircle}
          items={resolution.resolved}
          colorClass="text-brand-teal-600"
          borderClass="border-brand-teal-100 bg-brand-teal-50/50"
        />

        <FlagGroup
          title="Persisting"
          description="These issues remain"
          icon={AlertTriangle}
          items={resolution.persisting}
          colorClass="text-brand-amber-500"
          borderClass="border-brand-amber-100 bg-brand-amber-50/50"
        />

        <FlagGroup
          title="New"
          description="New concerns found"
          icon={XCircle}
          items={resolution.newFlags}
          colorClass="text-brand-red"
          borderClass="border-red-200 bg-red-50/50"
        />
      </div>
    </Card>
  );
}
