export type StatusTone = 'neutral' | 'success' | 'warning' | 'error' | 'info';

const reservationLabels: Record<string, string> = {
  PendingPayment: 'Pendiente de pago',
  Expired: 'Vencida',
  Confirmed: 'Confirmada',
  Completed: 'Completada',
  CancelledByTourist: 'Cancelada por el turista',
  CancelledByHost: 'Cancelada por el anfitrión',
  RefundPending: 'Reembolso pendiente',
  Refunded: 'Reembolsada',
};

export const getReservationStatusLabel = (status: string) => reservationLabels[status] ?? 'Estado no disponible';

export const getReservationStatusTone = (status: string): StatusTone => {
  if (status === 'Confirmed' || status === 'Completed' || status === 'Refunded') {
    return 'success';
  }
  if (status.startsWith('Cancelled') || status === 'Expired') return 'error';
  if (status === 'PendingPayment' || status === 'RefundPending') return 'warning';
  return 'info';
};
