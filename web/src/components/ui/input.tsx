import type { InputHTMLAttributes, TextareaHTMLAttributes } from 'react';
import { forwardRef } from 'react';

const baseStyles =
  'w-full px-3 py-2 bg-white rounded-input text-brand-slate-800 text-sm border border-brand-slate-200 focus:outline-none focus:border-brand-teal-400 focus:ring-[3px] focus:ring-brand-teal-50 transition-colors placeholder:text-brand-slate-300';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ label, id, className = '', ...props }, ref) => {
    const inputId = id || label?.toLowerCase().replace(/\s+/g, '-');
    return (
      <div>
        {label && (
          <label htmlFor={inputId} className="block text-[13px] font-medium text-brand-slate-600 mb-1">
            {label}
          </label>
        )}
        <input ref={ref} id={inputId} className={`${baseStyles} ${className}`} {...props} />
      </div>
    );
  }
);

Input.displayName = 'Input';

interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ label, id, className = '', ...props }, ref) => {
    const inputId = id || label?.toLowerCase().replace(/\s+/g, '-');
    return (
      <div>
        {label && (
          <label htmlFor={inputId} className="block text-[13px] font-medium text-brand-slate-600 mb-1">
            {label}
          </label>
        )}
        <textarea ref={ref} id={inputId} className={`${baseStyles} resize-none ${className}`} {...props} />
      </div>
    );
  }
);

Textarea.displayName = 'Textarea';

interface SelectProps extends InputHTMLAttributes<HTMLSelectElement> {
  label?: string;
  children: React.ReactNode;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  ({ label, id, className = '', children, ...props }, ref) => {
    const inputId = id || label?.toLowerCase().replace(/\s+/g, '-');
    return (
      <div>
        {label && (
          <label htmlFor={inputId} className="block text-[13px] font-medium text-brand-slate-600 mb-1">
            {label}
          </label>
        )}
        <select
          ref={ref as React.Ref<HTMLSelectElement>}
          id={inputId}
          className={`${baseStyles} ${className}`}
          {...(props as React.SelectHTMLAttributes<HTMLSelectElement>)}
        >
          {children}
        </select>
      </div>
    );
  }
);

Select.displayName = 'Select';
