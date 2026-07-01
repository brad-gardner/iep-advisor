interface RegisterPathCardProps {
  title: string;
  description: string;
  selected: boolean;
  onSelect: () => void;
  'data-testid'?: string;
}

export function RegisterPathCard({
  title,
  description,
  selected,
  onSelect,
  'data-testid': dataTestid,
}: RegisterPathCardProps) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      onClick={onSelect}
      data-testid={dataTestid}
      className={`w-full text-left rounded-card border p-4 transition-colors focus:outline-none focus:ring-[3px] focus:ring-brand-teal-50 ${
        selected
          ? 'border-brand-teal-400 bg-brand-teal-50'
          : 'border-brand-slate-200 bg-white hover:border-brand-teal-300'
      }`}
    >
      <p className="text-sm font-medium text-brand-slate-800">{title}</p>
      <p className="text-[13px] text-brand-slate-500 mt-1">{description}</p>
    </button>
  );
}
