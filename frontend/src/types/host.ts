export type HostVerificationStatus = 'Pending' | 'Approved' | 'Rejected' | 'Suspended';

export interface HostProfileRequest {
  displayName: string;
  description: string;
  phoneNumber: string;
}

export interface HostProfile {
  id: number;
  userId: number;
  userFullName: string;
  userEmail: string;
  displayName: string;
  description: string;
  phoneNumber: string;
  verificationStatus: HostVerificationStatus;
  rejectionReason: string | null;
  submittedAt: string;
  reviewedAt: string | null;
  reviewedByAdminId: number | null;
}

export type ExperienceApprovalStatus =
  | 'Draft'
  | 'PendingReview'
  | 'Approved'
  | 'Rejected'
  | 'Suspended';

export interface ManagedExperienceRequest {
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
  itinerary: import('./experience').ExperienceItineraryItem[];
  location: string;
  latitude: number | null;
  longitude: number | null;
  category: string;
  price: number;
  capacity: number;
  isUnlimitedCapacity: boolean;
}

export interface ManagedExperience extends ManagedExperienceRequest {
  id: number;
  slug: string;
  hostId: number;
  hostName: string;
  availableSpots: number;
  images: import('./experience').ExperienceImage[];
  approvalStatus: ExperienceApprovalStatus;
  rejectionReason: string | null;
  reviewedAt: string | null;
  reviewedByAdminId: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface HostDashboardSchedule {
  id: number;
  experienceId: number;
  experienceTitle: string;
  startsAt: string;
  endsAt: string;
  reservedSpots: number;
  capacity: number;
}

export interface HostDashboard {
  totalExperiences: number;
  publishedExperiences: number;
  upcomingSchedules: number;
  upcomingReservations: number;
  reservedSpots: number;
  completedReservations: number;
  netEarnings: number;
  currency: string;
  averageRating: number | null;
  reviewCount: number;
  nextSchedules: HostDashboardSchedule[];
}
