export type ScheduleStatus = 'Scheduled' | 'Closed' | 'Cancelled' | 'Completed';

export interface ExperienceSchedule {
  id: number;
  experienceId: number;
  startsAt: string;
  endsAt: string;
  capacity: number;
  availableSpots: number;
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
