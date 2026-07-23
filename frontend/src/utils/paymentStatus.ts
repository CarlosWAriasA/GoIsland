import type { StatusTone } from './reservationStatus';

// Etiquetas orientadas al usuario para los estados de pago del backend.
// Si llega un estado no mapeado, se muestra el valor tal cual.
const paymentLabels: Record<string, string> = {
  Pending: 'Pendiente',
  Paid: 'Pagado',
  Failed: 'Rechazado',
  Refunded: 'Reembolsado',
};

export const getPaymentStatusLabel = (status: string) => paymentLabels[status] ?? status;

// Tono semántico consistente con el resto de la aplicación.
export const getPaymentStatusTone = (status: string): StatusTone => {
  if (status === 'Paid') return 'success';
  if (status === 'Failed') return 'error';
  if (status === 'Pending') return 'warning';
  if (status === 'Refunded') return 'info';
  return 'neutral';
};
