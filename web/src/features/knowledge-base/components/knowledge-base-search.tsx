import { Search } from 'lucide-react';

interface KnowledgeBaseSearchProps {
  value: string;
  onChange: (value: string) => void;
}

export function KnowledgeBaseSearch({ value, onChange }: KnowledgeBaseSearchProps) {
  return (
    <div className="relative">
      <Search
        className="absolute left-3 top-1/2 -translate-y-1/2 text-brand-slate-300"
        size={18}
        strokeWidth={1.8}
        aria-hidden="true"
      />
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="Search knowledge base..."
        data-testid="kb-search"
        className="w-full pl-10 pr-3 py-2 bg-white rounded-input text-brand-slate-800 text-sm border border-brand-slate-200 focus:outline-none focus:border-brand-teal-400 focus:ring-[3px] focus:ring-brand-teal-50 transition-colors placeholder:text-brand-slate-300"
      />
    </div>
  );
}
