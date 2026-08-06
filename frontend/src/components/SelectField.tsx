import { ChevronDown, CircleAlert } from 'lucide-react';
import { useId } from 'react';
import type { SelectHTMLAttributes } from 'react';

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  error?: string;
  hint?: string;
}

export const SelectField = ({
  label,
  error,
  hint,
  id,
  className = '',
  'aria-describedby': ariaDescribedBy,
  ...props
}: SelectFieldProps) => {
  const generatedId = useId();
  const selectId = id || generatedId;
  const errorId = `${selectId}-error`;
  const hintId = `${selectId}-hint`;
  const describedBy = [ariaDescribedBy, error ? errorId : undefined, hint ? hintId : undefined]
    .filter(Boolean)
    .join(' ') || undefined;

  const { onKeyDown, ...restProps } = props;

  return (
    <div className="field-group">
      <label className="field-label" htmlFor={selectId}>
        {label}
        {props.required && <span className="field-required" aria-hidden="true">*</span>}
      </label>
      <div className="field-control">
        <select
          id={selectId}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          data-empty={props.value === '' || props.value === undefined ? 'true' : 'false'}
          className={`text-field select-field${error ? ' text-field--error' : ''} ${className}`}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
            }
            onKeyDown?.(event);
          }}
          {...restProps}
        />
        <ChevronDown className="select-field__icon" size={18} aria-hidden="true" />
      </div>
      {hint && <span className="field-hint" id={hintId}>{hint}</span>}
      {error && (
        <span className="field-error" id={errorId}>
          <CircleAlert size={14} aria-hidden="true" />
          {error}
        </span>
      )}
    </div>
  );
};

export default SelectField;
