import { CircleAlert, CircleCheck, Info, TriangleAlert } from 'lucide-react';
import type { ReactNode } from 'react';

interface AlertProps {
  children: ReactNode;
  tone?: 'info' | 'success' | 'warning' | 'error';
}

const icons = {
  info: Info,
  success: CircleCheck,
  warning: TriangleAlert,
  error: CircleAlert,
};

export const Alert = ({ children, tone = 'info' }: AlertProps) => {
  const Icon = icons[tone];
  return (
    <div className={`alert alert--${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      <Icon size={19} aria-hidden="true" />
      <div>{children}</div>
    </div>
  );
};

export default Alert;
