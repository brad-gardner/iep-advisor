import { sourceKey, type SourceOption } from "../types";

interface SourceCheckboxGroupProps {
  title: string;
  options: SourceOption[];
  selected: Set<string>;
  onToggle: (option: SourceOption) => void;
}

export function SourceCheckboxGroup({
  title,
  options,
  selected,
  onToggle,
}: SourceCheckboxGroupProps) {
  if (options.length === 0) return null;

  return (
    <fieldset className="space-y-2">
      <legend className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-1">
        {title}
      </legend>
      <div className="space-y-1.5">
        {options.map((option) => {
          const key = sourceKey(option.sourceType, option.sourceId);
          const id = `source-${key}`;
          return (
            <label
              key={key}
              htmlFor={id}
              className="flex items-center gap-2 text-sm text-brand-slate-600 cursor-pointer"
            >
              <input
                id={id}
                type="checkbox"
                checked={selected.has(key)}
                onChange={() => onToggle(option)}
                data-testid={`source-checkbox-${key}`}
                className="rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-500"
              />
              <span>{option.label}</span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
