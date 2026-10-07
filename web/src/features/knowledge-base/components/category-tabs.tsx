import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { CategoryCount } from '@/types/api';

interface CategoryTabsProps {
  categories: CategoryCount[];
  active: string | null;
  onChange: (category: string | null) => void;
}

// Known categories get a translated label (`knowledge-base:categories.<key>`);
// an unrecognized one (new DB category this UI hasn't been taught yet) falls
// back to its own raw name rather than a blank or missing-key error. A
// `switch` over literal keys, not a template-literal lookup, so each call
// resolves against a real key `tsc` can check.
function labelFor(category: string, t: TFunction<'knowledge-base'>): string {
  switch (category) {
    case 'rights':
      return t('categories.rights');
    case 'provisions':
      return t('categories.provisions');
    case 'glossary':
      return t('categories.glossary');
    case 'process':
      return t('categories.process');
    case 'tips':
      return t('categories.tips');
    default:
      return category;
  }
}

export function CategoryTabs({ categories, active, onChange }: CategoryTabsProps) {
  const { t } = useTranslation('knowledge-base');
  const totalCount = categories.reduce((sum, c) => sum + c.count, 0);

  return (
    <div className="flex gap-1 overflow-x-auto pb-1">
      <button
        onClick={() => onChange(null)}
        data-testid="kb-tab-all"
        className={`shrink-0 px-3 py-1.5 rounded-button text-sm font-medium transition-colors ${
          active === null
            ? 'bg-brand-teal-50 text-brand-teal-600 border border-brand-teal-200'
            : 'text-brand-slate-500 hover:text-brand-slate-700 hover:bg-brand-slate-50 border border-transparent'
        }`}
      >
        {t('categories.all')}
        <span className="ml-1.5 text-xs opacity-70">{totalCount}</span>
      </button>

      {categories.map(({ category, count }) => (
        <button
          key={category}
          onClick={() => onChange(category)}
          data-testid={`kb-tab-${category}`}
          className={`shrink-0 px-3 py-1.5 rounded-button text-sm font-medium transition-colors ${
            active === category
              ? 'bg-brand-teal-50 text-brand-teal-600 border border-brand-teal-200'
              : 'text-brand-slate-500 hover:text-brand-slate-700 hover:bg-brand-slate-50 border border-transparent'
          }`}
        >
          {labelFor(category, t)}
          <span className="ml-1.5 text-xs opacity-70">{count}</span>
        </button>
      ))}
    </div>
  );
}
