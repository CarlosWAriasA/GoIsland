import type { CSSProperties } from 'react';

interface LogoProps {
  showUnderline?: boolean;
  fontSize?: string;
  variant?: 'primary' | 'light';
  iconOnly?: boolean;
  style?: CSSProperties;
}

export const Logo = ({
  showUnderline = false,
  fontSize = '1.5rem',
  variant = 'primary',
  iconOnly = false,
  style,
}: LogoProps) => {
  const logoStyle = { '--logo-font-size': fontSize, ...style } as CSSProperties;

  return (
    <span className={`logo logo--${variant}${iconOnly ? ' logo--icon-only' : ''}`} style={logoStyle}>
      {!iconOnly && (
        <span className="logo-text">
          <span className="logo-text__go">Go</span>
          <span className="logo-text__island">Island</span>
        </span>
      )}
      <span className="logo-isotype" aria-hidden={iconOnly ? undefined : true} role={iconOnly ? 'img' : undefined} aria-label={iconOnly ? 'GoIsland' : undefined}>
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100%" height="100%">
          <path d="M 50 15 C 33.4 15 20 28.4 20 45 C 20 62 40 78 50 85 C 56.5 80.5 70 70 76.5 60 M 80 45 C 80 37 76.7 30 71.3 25" fill="none" stroke="currentColor" strokeWidth="10" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M 78 48 L 56 48" fill="none" stroke="currentColor" strokeWidth="10" strokeLinecap="round" strokeLinejoin="round" />
          <circle cx="50" cy="33" r="6" fill="var(--color-coral-500)" />
          <path d="M 50 44 L 50 60" fill="none" stroke="var(--color-coral-500)" strokeWidth="8" strokeLinecap="round" />
        </svg>
      </span>
      {showUnderline && <span className="logo-underline" />}
    </span>
  );
};

export default Logo;
