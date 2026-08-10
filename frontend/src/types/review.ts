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

export type ReviewModerationStatus = 'Visible' | 'Hidden' | 'Deleted' | 'Reported';

export interface AdminReviewListParams {
  query?: string;
  status?: ReviewModerationStatus;
  page?: number;
  pageSize?: number;
}
