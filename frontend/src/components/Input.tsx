import React, { useId } from 'react';

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  icon?: React.ReactNode;
}

export const Input: React.FC<InputProps> = ({
  label,
  error,
  icon,
  className = '',
  id,
  style,
  ...props
}) => {
  const generatedId = useId();
  const inputId = id || generatedId;
  const errorId = `${inputId}-error`;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', width: '100%', marginBottom: '16px', ...style }}>
      {label && (
        <label
          htmlFor={inputId}
          style={{
            fontSize: '0.85rem',
            fontWeight: 600,
            color: 'var(--color-text)',
            fontFamily: "'Poppins', sans-serif",
            letterSpacing: '0.01em',
            paddingLeft: '2px',
          }}
        >
          {label}
        </label>
      )}
      
      <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
        {icon && (
          <div
            style={{
              position: 'absolute',
              left: '14px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--color-text-muted)',
              pointerEvents: 'none',
            }}
          >
            {icon}
          </div>
        )}
        
        <input
          id={inputId}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : props['aria-describedby']}
          className={`input-glass ${className}`}
          style={{
            width: '100%',
            padding: '12px 16px',
            paddingLeft: icon ? '42px' : '16px',
            background: 'var(--color-white)',
            border: error ? '1px solid #ef4444' : '1px solid var(--color-border)',
            borderRadius: '8px',
            color: 'var(--color-text)',
            fontSize: '0.95rem',
            fontFamily: "'Poppins', sans-serif",
            outline: 'none',
            transition: 'all 0.2s cubic-bezier(0.4, 0, 0.2, 1)',
            boxShadow: 'none',
          }}
          {...props}
        />
      </div>

      {error && (
        <span
          id={errorId}
          style={{
            fontSize: '0.8rem',
            color: '#ef4444',
            paddingLeft: '4px',
            marginTop: '2px',
            display: 'flex',
            alignItems: 'center',
            gap: '4px',
            fontFamily: "'Poppins', sans-serif",
          }}
        >
          <svg style={{ width: '12px', height: '12px' }} fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
          </svg>
          {error}
        </span>
      )}

      <style>{`
        .input-glass:focus {
          border-color: ${error ? '#ef4444' : 'var(--color-primary)'} !important;
          box-shadow: 0 0 0 3px ${error ? 'rgba(239, 68, 68, 0.15)' : 'rgba(0, 86, 179, 0.12)'} !important;
          background: var(--color-white) !important;
        }
        .input-glass::placeholder {
          color: var(--color-text-muted);
          opacity: 0.6;
        }
      `}</style>
    </div>
  );
};
export default Input;
