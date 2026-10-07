import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import type { KnowledgeBaseEntry } from '@/types/api';

interface KnowledgeBaseEntryCardProps {
  entry: KnowledgeBaseEntry;
  /** The entry a deep link (`/knowledge-base/:entryId`) points at. */
  highlighted?: boolean;
}

export function KnowledgeBaseEntryCard({ entry, highlighted = false }: KnowledgeBaseEntryCardProps) {
  // Only the chrome around this card is translated — `entry.title`,
  // `entry.content`, `entry.legalReference`, `entry.state` and `entry.tags`
  // are the article's own DB content and stay exactly as authored (future
  // work; see the plan's knowledge-base article-translation follow-up). The
  // note below is this phase's stand-in: tell a Spanish reader plainly that
  // the article itself is still English.
  const { t, i18n } = useTranslation('knowledge-base');
  // The article's own title/content stay English regardless of the UI
  // language (see the comment above) — marking them `lang="en"` whenever the
  // UI itself isn't English tells assistive tech (and the browser's own
  // translate/pronunciation heuristics) that this text is a different
  // language than the surrounding page, same as `<html lang>` does for the
  // page as a whole.
  const articleLang = i18n.resolvedLanguage === 'en' ? undefined : 'en';
  return (
    <Card
      id={`kb-entry-${entry.id}`}
      className={highlighted ? 'ring-2 ring-brand-teal-300 scroll-mt-20' : ''}
      data-testid="kb-entry"
      data-highlighted={highlighted ? 'true' : undefined}
    >
      {entry.legalReference && (
        <p className="text-[10px] font-medium uppercase tracking-wider text-brand-teal-500 mb-1.5">{entry.legalReference}</p>
      )}

      <h3 className="font-serif text-lg text-brand-slate-800 mb-2" lang={articleLang}>
        {entry.title}
      </h3>

      {i18n.resolvedLanguage === 'es' && (
        <p className="text-xs italic text-brand-slate-500 mb-2" data-testid="kb-entry-english-note">
          {t('entryCard.availableInEnglish')}
        </p>
      )}

      <p className="text-sm text-brand-slate-600 leading-relaxed mb-3" lang={articleLang}>
        {entry.content}
      </p>

      <div className="flex flex-wrap gap-1.5">
        {entry.state && <Badge variant="warning">{entry.state}</Badge>}
        {entry.tags.map((tag) => (
          <Badge key={tag} variant="neutral">
            {tag}
          </Badge>
        ))}
      </div>
    </Card>
  );
}
