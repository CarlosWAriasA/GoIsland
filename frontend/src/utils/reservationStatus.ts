export type StatusTone = 'neutral' | 'success' | 'warning' | 'error' | 'info';

const reservationLabels: Record<string, string> = {
  PendingPayment: 'Pendiente de pago',
  Confirmed: 'Confirmada',
  Paid: 'Pagada',
  Completed: 'Completada',
  Cancelled: 'Cancelada',
  CancelledByHost: 'Cancelada por el anfitrión',
  CancelledByUser: 'Cancelada por el turista',
  RefundPending: 'Reembolso pendiente',
  Refunded: 'Reembolsada',
};

export const getReservationStatusLabel = (status: string) => reservationLabels[status] ?? 'Estado no disponible';

export const getReservationStatusTone = (status: string): StatusTone => {
  if (status === 'Confirmed' || status === 'Paid' || status === 'Completed' || status === 'Refunded') {
    return 'success';
  }
  if (status.startsWith('Cancelled')) return 'error';
  if (status === 'PendingPayment' || status === 'RefundPending') return 'warning';
  return 'info';
};
