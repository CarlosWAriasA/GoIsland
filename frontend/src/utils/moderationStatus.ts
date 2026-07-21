import type { ExperienceApprovalStatus, HostVerificationStatus } from '../types';

export type StatusTone = 'neutral' | 'success' | 'warning' | 'error' | 'info';

const labels: Record<HostVerificationStatus | ExperienceApprovalStatus, string> = {
  Pending: 'Pendiente',
  Approved: 'Aprobado',
  Rejected: 'Rechazado',
  Suspended: 'Suspendido',
  Draft: 'Borrador',
  PendingReview: 'En revisión',
};

export const getModerationLabel = (status: HostVerificationStatus | ExperienceApprovalStatus) => (
  labels[status]
);

export const getModerationTone = (
  status: HostVerificationStatus | ExperienceApprovalStatus,
): StatusTone => {
  if (status === 'Approved') return 'success';
  if (status === 'Rejected' || status === 'Suspended') return 'error';
  if (status === 'Pending' || status === 'PendingReview') return 'warning';
  return 'neutral';
};
