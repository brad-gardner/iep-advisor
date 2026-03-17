import type { CategoryCount } from '@/types/api';

interface CategoryTabsProps {
  categories: CategoryCount[];
  active: string | null;
  onChange: (category: string | null) => void;
}

const categoryLabels: Record<string, string> = {
  rights: 'Rights',
  provisions: 'Provisions',
  glossary: 'Glossary',
  process: 'Process',
  tips: 'Tips',
};

function labelFor(category: string): string {
  return categoryLabels[category] ?? category;
}

export function CategoryTabs({ categories, active, onChange }: CategoryTabsProps) {
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
        All
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
          {labelFor(category)}
          <span className="ml-1.5 text-xs opacity-70">{count}</span>
        </button>
      ))}
    </div>
  );
}
