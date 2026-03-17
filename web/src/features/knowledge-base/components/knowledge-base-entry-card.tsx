import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import type { KnowledgeBaseEntry } from '@/types/api';

interface KnowledgeBaseEntryCardProps {
  entry: KnowledgeBaseEntry;
}

export function KnowledgeBaseEntryCard({ entry }: KnowledgeBaseEntryCardProps) {
  return (
    <Card data-testid="kb-entry">
      {entry.legalReference && (
        <p className="text-[10px] font-medium uppercase tracking-wider text-brand-teal-500 mb-1.5">
          {entry.legalReference}
        </p>
      )}

      <h3 className="font-serif text-lg text-brand-slate-800 mb-2">
        {entry.title}
      </h3>

      <p className="text-sm text-brand-slate-600 leading-relaxed mb-3">
        {entry.content}
      </p>

      <div className="flex flex-wrap gap-1.5">
        {entry.state && (
          <Badge variant="warning">{entry.state}</Badge>
        )}
        {entry.tags.map((tag) => (
          <Badge key={tag} variant="neutral">{tag}</Badge>
        ))}
      </div>
    </Card>
  );
}
