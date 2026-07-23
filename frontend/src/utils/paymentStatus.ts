import type { StatusTone } from './reservationStatus';

const paymentLabels: Record<string, string> = {
  Pending: 'Pendiente',
  Paid: 'Pagado',
  Failed: 'Rechazado',
  Refunded: 'Reembolsado',
};

export const getPaymentStatusLabel = (status: string) => paymentLabels[status] ?? status;

export const getPaymentStatusTone = (status: string): StatusTone => {
  if (status === 'Paid') return 'success';
  if (status === 'Failed') return 'error';
  if (status === 'Pending') return 'warning';
  if (status === 'Refunded') return 'info';
  return 'neutral';
};
