import { CircleAlert, Eye, EyeOff } from 'lucide-react';
import { useId, useState } from 'react';
import type { InputHTMLAttributes, ReactNode } from 'react';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  hint?: string;
  icon?: ReactNode;
  passwordToggleLabel?: string;
}

export const Input = ({
  label,
  error,
  hint,
  icon,
  passwordToggleLabel,
  className = '',
  id,
  type,
  'aria-describedby': ariaDescribedBy,
  ...props
}: InputProps) => {
  const generatedId = useId();
  const [passwordVisible, setPasswordVisible] = useState(false);
  const inputId = id || generatedId;
  const errorId = `${inputId}-error`;
  const hintId = `${inputId}-hint`;
  const describedBy = [ariaDescribedBy, error ? errorId : undefined, hint ? hintId : undefined]
    .filter(Boolean)
    .join(' ') || undefined;
  const hasPasswordToggle = type === 'password';
  const inputType = hasPasswordToggle && passwordVisible ? 'text' : type;
  const toggleLabel = passwordToggleLabel || label?.toLowerCase() || 'contraseña';

  return (
    <div className="field-group">
      {label && (
        <label className="field-label" htmlFor={inputId}>
          {label}
          {props.required && <span className="field-required" aria-hidden="true">*</span>}
        </label>
      )}
      <div className="field-control">
        {icon && <span className="field-icon" aria-hidden="true">{icon}</span>}
        <input
          id={inputId}
          type={inputType}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          className={`text-field${icon ? ' text-field--with-icon' : ''}${hasPasswordToggle ? ' text-field--with-toggle' : ''}${error ? ' text-field--error' : ''} ${className}`}
          {...props}
        />
        {hasPasswordToggle && (
          <button
            type="button"
            className="field-password-toggle"
            aria-label={`${passwordVisible ? 'Ocultar' : 'Mostrar'} ${toggleLabel}`}
            aria-pressed={passwordVisible}
            onClick={() => setPasswordVisible((visible) => !visible)}
          >
            {passwordVisible ? <EyeOff size={18} aria-hidden="true" /> : <Eye size={18} aria-hidden="true" />}
          </button>
        )}
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

export default Input;
