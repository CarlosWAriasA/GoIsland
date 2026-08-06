ALTER TABLE reservations
    ADD COLUMN IF NOT EXISTS expires_at timestamptz;

UPDATE reservations
SET expires_at = reservation_date + interval '15 minutes'
WHERE status = 'PendingPayment'
  AND expires_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_reservations_pending_expiration
    ON reservations (expires_at, id)
    WHERE status = 'PendingPayment';

ALTER TABLE reservation_status_history
    ALTER COLUMN changed_by_user_id DROP NOT NULL;

ALTER TABLE reservations
    DROP CONSTRAINT IF EXISTS ck_reservations_lifecycle_status;

ALTER TABLE reservations
    ADD CONSTRAINT ck_reservations_lifecycle_status CHECK (
        status IN (
            'PendingPayment', 'Expired', 'Confirmed', 'CancelledByTourist', 'CancelledByHost',
            'Completed', 'RefundPending', 'Refunded'
        )
    );
