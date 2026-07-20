export interface Reservation {
  id: number;
  userId: number;
  experienceId: number;
  quantity: number;
  status: string;
  totalAmount: number;
  reservationDate: string;
}

export interface CreateReservationRequest {
  experienceId: number;
  quantity: number;
}
