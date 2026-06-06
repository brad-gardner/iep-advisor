import { WORKSPACE_TABS, type WorkspaceTab } from '../lib/workspace-tabs';

interface SectionNavigatorProps {
  active: WorkspaceTab;
  onChange: (tab: WorkspaceTab) => void;
}

export function SectionNavigator({ active, onChange }: SectionNavigatorProps) {
  return (
    <nav
      className="flex flex-wrap gap-1 border-b border-brand-slate-200"
      aria-label="IEP sections"
      data-testid="section-navigator"
    >
      {WORKSPACE_TABS.map((tab) => {
        const isActive = tab.id === active;
        return (
          <button
            key={tab.id}
            type="button"
            onClick={() => onChange(tab.id)}
            aria-current={isActive ? 'page' : undefined}
            className={`px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
              isActive
                ? 'border-brand-teal-500 text-brand-teal-600'
                : 'border-transparent text-brand-slate-500 hover:text-brand-slate-700'
            }`}
            data-testid={`tab-${tab.id}`}
          >
            {tab.label}
          </button>
        );
      })}
    </nav>
  );
}
