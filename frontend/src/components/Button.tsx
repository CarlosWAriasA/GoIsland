import type { ButtonHTMLAttributes } from 'react';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'accent' | 'secondary' | 'outline' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  isLoading?: boolean;
  fullWidth?: boolean;
}

export const Button = ({
  children,
  variant = 'primary',
  size = 'md',
  isLoading = false,
  fullWidth = false,
  className = '',
  disabled,
  type = 'button',
  ...props
}: ButtonProps) => (
  <button
    type={type}
    disabled={disabled || isLoading}
    aria-busy={isLoading || undefined}
    className={`btn-custom btn-${variant} btn-${size} ${fullWidth ? 'btn-full' : ''} ${className}`}
    {...props}
  >
    {isLoading ? (
      <span className="btn-loading">
        <svg className="btn-spinner" aria-hidden="true" fill="none" viewBox="0 0 24 24">
          <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" opacity="0.25" />
          <path
            fill="currentColor"
            opacity="0.75"
            d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
          />
        </svg>
        Cargando…
      </span>
    ) : children}
  </button>
);

export default Button;
