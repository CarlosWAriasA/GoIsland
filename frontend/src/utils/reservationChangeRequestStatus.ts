import type { StatusTone } from './reservationStatus';

const statusLabels: Record<string, string> = {
  Pending: 'Pendiente de revisión',
  Approved: 'Aprobada',
  Rejected: 'Rechazada',
};

export const getChangeRequestStatusLabel = (status: string) => statusLabels[status] ?? 'Estado no disponible';

export const getChangeRequestStatusTone = (status: string): StatusTone => {
  if (status === 'Approved') return 'success';
  if (status === 'Rejected') return 'error';
  return 'warning';
};

const typeLabels: Record<string, string> = {
  Cancel: 'Cancelación y reembolso',
  Reschedule: 'Reprogramación',
};

export const getChangeRequestTypeLabel = (type: string) => typeLabels[type] ?? type;
