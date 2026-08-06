CREATE UNIQUE INDEX IF NOT EXISTS ux_experience_schedules_experience_start
    ON experience_schedules (experience_id, starts_at);
