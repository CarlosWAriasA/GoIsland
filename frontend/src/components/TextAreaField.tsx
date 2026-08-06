import { CircleAlert } from 'lucide-react';
import { useId } from 'react';
import type { TextareaHTMLAttributes } from 'react';

interface TextAreaFieldProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string;
  error?: string;
  hint?: string;
}

export const TextAreaField = ({
  label,
  error,
  hint,
  id,
  className = '',
  'aria-describedby': ariaDescribedBy,
  ...props
}: TextAreaFieldProps) => {
  const generatedId = useId();
  const fieldId = id || generatedId;
  const errorId = `${fieldId}-error`;
  const hintId = `${fieldId}-hint`;
  const describedBy = [ariaDescribedBy, error ? errorId : undefined, hint ? hintId : undefined]
    .filter(Boolean)
    .join(' ') || undefined;

  return (
    <div className="field-group">
      <label className="field-label" htmlFor={fieldId}>
        {label}
        {props.required && <span className="field-required" aria-hidden="true">*</span>}
      </label>
      <textarea
        id={fieldId}
        className={`text-field text-area-field${error ? ' text-field--error' : ''} ${className}`}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        {...props}
      />
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

export default TextAreaField;
