export interface Review {
  id: number;
  reservationId: number;
  userId: number;
  authorName: string;
  experienceId: number;
  hostId: number;
  rating: number;
  comment: string;
  moderationStatus: string;
  createdAt: string;
  updatedAt: string;
}

export interface ReviewInput {
  rating: number;
  comment: string;
}
