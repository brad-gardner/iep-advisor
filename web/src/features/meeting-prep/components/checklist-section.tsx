import type { LucideIcon } from 'lucide-react';
import type { ChecklistItem } from '@/types/api';
import { ChecklistItemRow } from './checklist-item-row';

interface ChecklistSectionProps {
  title: string;
  icon: LucideIcon;
  items: ChecklistItem[];
  section: string;
  onCheck: (index: number, isChecked: boolean) => void;
}

export function ChecklistSection({ title, icon: Icon, items, section: _section, onCheck }: ChecklistSectionProps) {
  if (items.length === 0) return null;

  const checkedCount = items.filter((i) => i.isChecked).length;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Icon className="w-5 h-5 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800">
            {title}
          </h3>
        </div>
        <span className="text-[12px] text-brand-slate-400 font-medium">
          {checkedCount} of {items.length} completed
        </span>
      </div>

      <div className="space-y-2">
        {items.map((item, index) => (
          <ChecklistItemRow
            key={index}
            item={item}
            index={index}
            onCheck={onCheck}
          />
        ))}
      </div>
    </div>
  );
}
