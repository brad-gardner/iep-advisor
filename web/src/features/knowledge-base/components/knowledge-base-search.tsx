import { useTranslation } from 'react-i18next';
import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';

interface KnowledgeBaseSearchProps {
  value: string;
  onChange: (value: string) => void;
}

export function KnowledgeBaseSearch({ value, onChange }: KnowledgeBaseSearchProps) {
  const { t } = useTranslation('knowledge-base');
  return (
    <div className="relative">
      <Search
        className="absolute left-3 top-1/2 -translate-y-1/2 z-10 text-brand-slate-300"
        size={18}
        strokeWidth={1.8}
        aria-hidden="true"
      />
      <Input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={t('search.placeholder')}
        aria-label={t('search.ariaLabel')}
        data-testid="kb-search"
        className="!pl-10"
      />
    </div>
  );
}
