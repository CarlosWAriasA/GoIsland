BEGIN;

CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS ix_experiences_public_created_at
    ON experiences (approval_status, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_experiences_host_updated_at
    ON experiences (host_id, updated_at DESC);

CREATE INDEX IF NOT EXISTS ix_experiences_moderation_updated_at
    ON experiences (approval_status, updated_at);

CREATE INDEX IF NOT EXISTS ix_host_profiles_moderation_submitted_at
    ON host_profiles (verification_status, submitted_at);

CREATE INDEX IF NOT EXISTS ix_experiences_public_price
    ON experiences (price, created_at DESC)
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_public_category
    ON experiences (lower(category))
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_public_difficulty
    ON experiences (lower(difficulty))
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_title_trgm
    ON experiences USING gin (title gin_trgm_ops)
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_summary_trgm
    ON experiences USING gin (short_description gin_trgm_ops)
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_location_trgm
    ON experiences USING gin (location gin_trgm_ops)
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_tags_gin
    ON experiences USING gin (tags)
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_languages_gin
    ON experiences USING gin (languages)
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_schedules_public_availability
    ON experience_schedules (experience_id, starts_at, available_spots)
    WHERE status = 'Scheduled';

CREATE INDEX IF NOT EXISTS ix_reservations_user_created_at
    ON reservations (user_id, reservation_date DESC);

CREATE INDEX IF NOT EXISTS ix_reservations_user_status_created_at
    ON reservations (user_id, status, reservation_date DESC);

CREATE INDEX IF NOT EXISTS ix_reservations_experience_created_at
    ON reservations (experience_id, reservation_date DESC);

COMMIT;
