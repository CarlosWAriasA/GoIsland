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

export type ReservationChangeRequestType = 'Cancel' | 'Reschedule';
export type ReservationChangeRequestStatus = 'Pending' | 'Approved' | 'Rejected';

export interface ReservationChangeRequest {
  id: number;
  reservationId: number;
  requestedByUserId: number;
  type: ReservationChangeRequestType;
  status: ReservationChangeRequestStatus;
  reason: string;
  requestedScheduleId: number | null;
  requestedScheduleStartsAt: string | null;
  reviewedByUserId: number | null;
  reviewedAt: string | null;
  decisionReason: string | null;
  createdAt: string;
  experienceTitle: string;
  reservationStartsAt: string;
  quantity: number;
}

export interface Reservation {
  id: number;
  userId: number;
  experienceId: number;
  scheduleId: number;
  experienceSlug: string;
  experienceTitle: string;
  experienceLocation: string;
  latitude: number | null;
  longitude: number | null;
  schedulingMode: string;
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
  pendingChangeRequest: ReservationChangeRequest | null;
}

export interface CreateReservationRequest {
  scheduleId: number;
  quantity: number;
}

export interface CreateSelfScheduledReservationRequest {
  experienceId: number;
  startsAtLocal: string;
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
