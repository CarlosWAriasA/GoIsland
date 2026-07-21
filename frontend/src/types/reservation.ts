export type ReservationStatus =
  | 'PendingPayment'
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
  experienceTitle: string;
  experienceLocation: string;
  startsAt: string;
  endsAt: string;
  quantity: number;
  status: ReservationStatus;
  totalAmount: number;
  reservationDate: string;
  updatedAt: string;
  cancelledAt: string | null;
  statusHistory: ReservationStatusHistory[];
}

export interface CreateReservationRequest {
  scheduleId: number;
  quantity: number;
}
