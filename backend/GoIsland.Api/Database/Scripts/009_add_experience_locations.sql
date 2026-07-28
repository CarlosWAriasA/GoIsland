ALTER TABLE experiences
    ADD COLUMN IF NOT EXISTS latitude numeric(9, 6),
    ADD COLUMN IF NOT EXISTS longitude numeric(9, 6);

ALTER TABLE experiences
    DROP CONSTRAINT IF EXISTS ck_experiences_coordinates;

ALTER TABLE experiences
    ADD CONSTRAINT ck_experiences_coordinates CHECK (
        (latitude IS NULL AND longitude IS NULL)
        OR (
            latitude BETWEEN -90 AND 90
            AND longitude BETWEEN -180 AND 180
        )
    );

CREATE INDEX IF NOT EXISTS ix_experiences_coordinates
    ON experiences (latitude, longitude)
    WHERE latitude IS NOT NULL AND longitude IS NOT NULL;
