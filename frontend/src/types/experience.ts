export interface Experience {
  id: number;
  title: string;
  description: string;
  location: string;
  category: string;
  price: number;
  capacity: number;
  availableSpots: number;
  isApproved: boolean;
  createdAt: string;
  averageRating: number | null;
  reviewCount: number;
}

export interface ExperienceSearchParams {
  location?: string;
  category?: string;
  minPrice?: number;
  maxPrice?: number;
  from?: string;
  to?: string;
  quantity?: number;
}
