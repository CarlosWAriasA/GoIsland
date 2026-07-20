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
}

export interface ExperienceSearchParams {
  location?: string;
  category?: string;
  maxPrice?: number;
}
