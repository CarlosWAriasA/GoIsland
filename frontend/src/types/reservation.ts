export type ReservationStatus =
  | 'PendingPayment'
  | 'Expired'
  | 'Confirmed'
  | 'CancelledByTourist'
  | 'CancelledByHost'
  | 'Completed'
  | 'RefundPending'
  | 'Refunded';

export interface ReservationStatusHistory {
  fromStatus: string | null;
  toStatus: string;
  reason: string | null;
  createdAt: string;
}

export interface Reservation {
  id: number;
  userId: number;
  experienceId: number;
  scheduleId: number;
  experienceSlug: string;
  experienceTitle: string;
  experienceLocation: string;
  startsAt: string;
  endsAt: string;
  quantity: number;
  status: ReservationStatus;
  totalAmount: number;
  reservationDate: string;
  expiresAt: string | null;
  updatedAt: string;
  cancelledAt: string | null;
  statusHistory: ReservationStatusHistory[];
}

export interface CreateReservationRequest {
  scheduleId: number;
  quantity: number;
}

export interface ReservationListParams {
  query?: string;
  status?: ReservationStatus;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}
