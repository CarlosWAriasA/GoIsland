ALTER TABLE experiences
    ADD COLUMN IF NOT EXISTS scheduling_mode varchar(20) NOT NULL DEFAULT 'HostScheduled';

ALTER TABLE experiences
    DROP CONSTRAINT IF EXISTS ck_experiences_scheduling_mode;

ALTER TABLE experiences
    ADD CONSTRAINT ck_experiences_scheduling_mode CHECK (
        scheduling_mode IN ('HostScheduled', 'SelfGuided')
    );

UPDATE experiences
SET scheduling_mode = 'SelfGuided'
FROM users
WHERE experiences.host_id = users.id
  AND users.email = 'catalogo@goisland.invalid'
  AND experiences.scheduling_mode = 'HostScheduled';
