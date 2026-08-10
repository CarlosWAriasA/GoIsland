import type { StatusTone } from './reservationStatus';

const reviewModerationLabels: Record<string, string> = {
  Visible: 'Publicada',
  Hidden: 'Oculta',
  Deleted: 'Eliminada',
  Reported: 'Reportada',
};

export const getReviewModerationLabel = (status: string) => reviewModerationLabels[status] ?? status;

export const getReviewModerationTone = (status: string): StatusTone => {
  if (status === 'Visible') return 'success';
  if (status === 'Reported') return 'warning';
  if (status === 'Hidden' || status === 'Deleted') return 'error';
  return 'neutral';
};
