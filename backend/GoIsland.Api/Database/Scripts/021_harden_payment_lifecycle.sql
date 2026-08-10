-- Endurece cancelaciones, pagos tardios y reembolsos recuperables.

alter table refunds add column if not exists failure_code varchar(80) null;
alter table refunds add column if not exists attempt_count integer not null default 0;
alter table refunds add column if not exists updated_at timestamptz null;

update refunds set updated_at = created_at where updated_at is null;
alter table refunds alter column updated_at set not null;

alter table payments drop constraint if exists ck_payments_status;
alter table payments add constraint ck_payments_status check (
    status in ('Pending', 'Paid', 'Failed', 'RefundPending', 'Refunded')
);

alter table refunds drop constraint if exists ck_refunds_status;
alter table refunds add constraint ck_refunds_status check (
    status in ('Pending', 'Failed', 'Partial', 'Completed')
);
alter table refunds drop constraint if exists ck_refunds_attempt_count;
alter table refunds add constraint ck_refunds_attempt_count check (attempt_count >= 0);

alter table payment_gateway_attempts drop constraint if exists ck_payment_gateway_attempts_outcome;
alter table payment_gateway_attempts add constraint ck_payment_gateway_attempts_outcome check (
    outcome in ('Created', 'Approved', 'Rejected', 'Cancelled', 'RefundRequested', 'RefundFailed', 'Refunded')
);

drop index if exists ux_payments_active_reservation;
create unique index ux_payments_active_reservation
    on payments (reservation_id) where status in ('Pending', 'Paid', 'RefundPending');

create index if not exists ix_refunds_status_updated_at on refunds (status, updated_at);
