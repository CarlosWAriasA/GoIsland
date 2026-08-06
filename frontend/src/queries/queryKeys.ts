import type { ExperienceSearchParams } from '../types';

export const queryRefresh = {
  availability: 15_000,
  catalog: 30_000,
} as const;

export const experienceKeys = {
  all: ['experiences'] as const,
  featured: () => [...experienceKeys.all, 'featured'] as const,
  map: () => [...experienceKeys.all, 'map'] as const,
  searches: () => [...experienceKeys.all, 'search'] as const,
  search: (params: ExperienceSearchParams) => [...experienceKeys.searches(), params] as const,
  details: () => [...experienceKeys.all, 'detail'] as const,
  detail: (identifier: number | string) => [...experienceKeys.details(), identifier] as const,
  availability: (experienceId: number) => [
    ...experienceKeys.all,
    'availability',
    experienceId,
  ] as const,
  reviews: (experienceId: number) => [...experienceKeys.all, 'reviews', experienceId] as const,
};

export const reservationKeys = {
  all: ['reservations'] as const,
};
