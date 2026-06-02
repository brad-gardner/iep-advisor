import type { LinkableChild } from '../types';

// Sentinel value for the "create a new child profile" radio option.
export const CREATE_NEW = 'create-new';

interface ChildLinkChoiceProps {
  existingChildren: LinkableChild[];
  value: string;
  onChange: (value: string) => void;
}

export function ChildLinkChoice({ existingChildren, value, onChange }: ChildLinkChoiceProps) {
  return (
    <fieldset className="space-y-2 text-left" data-testid="child-link-choice">
      <legend className="text-[13px] font-medium text-brand-slate-600 mb-1">
        How should we link this student?
      </legend>

      {existingChildren.map((child) => {
        const optionValue = String(child.childProfileId);
        return (
          <label
            key={child.childProfileId}
            className="flex items-center gap-3 p-3 rounded-input border border-brand-slate-200 cursor-pointer hover:border-brand-teal-400"
          >
            <input
              type="radio"
              name="child-link-target"
              value={optionValue}
              checked={value === optionValue}
              onChange={() => onChange(optionValue)}
              data-testid={`child-link-option-${child.childProfileId}`}
            />
            <span className="text-sm text-brand-slate-800">
              Link to {child.firstName} {child.lastName ?? ''}
            </span>
          </label>
        );
      })}

      <label className="flex items-center gap-3 p-3 rounded-input border border-brand-slate-200 cursor-pointer hover:border-brand-teal-400">
        <input
          type="radio"
          name="child-link-target"
          value={CREATE_NEW}
          checked={value === CREATE_NEW}
          onChange={() => onChange(CREATE_NEW)}
          data-testid="child-link-option-create-new"
        />
        <span className="text-sm text-brand-slate-800">Create a new child profile</span>
      </label>
    </fieldset>
  );
}
