export type ScheduleStatus = 'Scheduled' | 'Closed' | 'Cancelled' | 'Completed';

export interface ExperienceSchedule {
  id: number;
  experienceId: number;
  startsAt: string;
  endsAt: string;
  capacity: number;
  availableSpots: number;
  isUnlimitedCapacity: boolean;
  status: ScheduleStatus;
  createdAt: string;
  updatedAt: string;
}

export interface CreateScheduleRequest {
  startsAt: string;
  endsAt: string;
  capacity: number;
}

export interface UpdateScheduleRequest extends CreateScheduleRequest {
  status: 'Scheduled' | 'Closed';
}

export interface RecurringScheduleRequest {
  startDate: string;
  endDate: string;
  startsAt: string;
  endsAt: string;
  weekdays: number[];
  capacity: number;
  excludedDates: string[];
}

export type RecurringScheduleDisposition = 'WillCreate' | 'Existing' | 'Excluded';

export interface RecurringSchedulePreviewItem {
  localDate: string;
  startsAt: string;
  endsAt: string;
  disposition: RecurringScheduleDisposition;
}

export interface RecurringSchedulePreview {
  timeZoneId: string;
  items: RecurringSchedulePreviewItem[];
  toCreate: number;
  existing: number;
  excluded: number;
}

export interface RecurringScheduleGeneration {
  created: number;
  existing: number;
  excluded: number;
  schedules: ExperienceSchedule[];
}

export interface CopyScheduleWeekRequest {
  sourceWeekStart: string;
  targetWeekStart: string;
}

export interface ScheduleBatchResponse {
  schedules: ExperienceSchedule[];
  conflictingScheduleIds: number[];
}
