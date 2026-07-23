export type StatusTone = 'neutral' | 'success' | 'warning' | 'error' | 'info';

// Etiquetas orientadas al usuario para los estados de reserva del backend.
// Si llega un estado no mapeado, se muestra el valor tal cual (sin romper nada).
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

export const getReservationStatusLabel = (status: string) => reservationLabels[status] ?? status;

// Tono semántico único y consistente en toda la app para los estados de reserva.
export const getReservationStatusTone = (status: string): StatusTone => {
  if (status === 'Confirmed' || status === 'Paid' || status === 'Completed' || status === 'Refunded') {
    return 'success';
  }
  if (status.startsWith('Cancelled')) return 'error';
  if (status === 'PendingPayment' || status === 'RefundPending') return 'warning';
  return 'info';
};
