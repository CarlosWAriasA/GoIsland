import type { ReactNode } from 'react';

interface StatusBadgeProps {
  children: ReactNode;
  tone?: 'neutral' | 'success' | 'warning' | 'error' | 'info';
}

export const StatusBadge = ({ children, tone = 'neutral' }: StatusBadgeProps) => (
  <span className={`status-badge status-badge--${tone}`}>{children}</span>
);

export default StatusBadge;
