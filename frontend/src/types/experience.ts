export interface ExperienceImage {
  id: number;
  url: string;
  cardUrl: string;
  thumbnailUrl: string;
  altText: string;
  creditText: string;
  creditUrl: string | null;
  licenseName: string | null;
  licenseUrl: string | null;
  isCover: boolean;
  sortOrder: number;
}

export interface Experience {
  id: number;
  slug: string;
  title: string;
  shortDescription: string;
  description: string;
  durationMinutes: number | null;
  timeZoneId: string;
  meetingPointInstructions: string;
  pickupInformation: string | null;
  whatIsIncluded: string[];
  whatIsNotIncluded: string[];
  whatToBring: string[];
  guestRequirements: string;
  minimumAge: number | null;
  difficulty: string;
  accessibilityInformation: string;
  languages: string[];
  cancellationPolicy: string;
  tags: string[];
  itinerary: ExperienceItineraryItem[];
  location: string;
  latitude: number | null;
  longitude: number | null;
  distanceKm: number | null;
  category: string;
  price: number;
  capacity: number;
  availableSpots: number;
  isUnlimitedCapacity: boolean;
  schedulingMode?: string;
  images: ExperienceImage[];
  isApproved: boolean;
  createdAt: string;
  averageRating: number | null;
  reviewCount: number;
}

export interface ExperienceItineraryItem {
  id?: number;
  title: string;
  description: string;
  durationMinutes: number;
  location: string | null;
  sortOrder?: number;
}

export interface ExperienceSearchParams {
  query?: string;
  location?: string;
  category?: string;
  minPrice?: number;
  maxPrice?: number;
  from?: string;
  to?: string;
  quantity?: number;
  language?: string;
  difficulty?: string;
  accessible?: boolean;
  sort?: 'relevance' | 'newest' | 'priceAsc' | 'priceDesc' | 'rating';
  page?: number;
  pageSize?: number;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}
