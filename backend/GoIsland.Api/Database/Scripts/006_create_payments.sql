-- Entrega C: ciclo financiero persistente con gateway reemplazable.
-- Amplia payments y crea intentos del gateway, eventos de webhook y reembolsos.

alter table payments add column if not exists user_id integer null;
alter table payments add column if not exists provider varchar(40) not null default 'Mock';
alter table payments add column if not exists provider_payment_id varchar(120) null;
alter table payments add column if not exists idempotency_key varchar(100) not null default '';
alter table payments add column if not exists request_hash varchar(64) not null default '';
alter table payments add column if not exists currency char(3) not null default 'USD';
alter table payments add column if not exists subtotal_amount numeric(10,2) null;
alter table payments add column if not exists service_fee_amount numeric(10,2) not null default 0;
alter table payments add column if not exists platform_commission_amount numeric(10,2) not null default 0;
alter table payments add column if not exists host_net_amount numeric(10,2) null;
alter table payments add column if not exists failure_code varchar(80) null;
alter table payments add column if not exists paid_at timestamptz null;
alter table payments add column if not exists refunded_amount numeric(10,2) null;
alter table payments add column if not exists updated_at timestamptz null;

-- Los pagos anteriores heredan el turista y los montos de su reserva.
update payments payment
set user_id = reservation.user_id
from reservations reservation
where reservation.id = payment.reservation_id
  and payment.user_id is null;

update payments set subtotal_amount = amount where subtotal_amount is null;
update payments set host_net_amount = amount where host_net_amount is null;
update payments set updated_at = created_at where updated_at is null;

alter table payments alter column user_id set not null;
alter table payments alter column subtotal_amount set not null;
alter table payments alter column host_net_amount set not null;
alter table payments alter column updated_at set not null;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'fk_payments_user_id') then
        alter table payments add constraint fk_payments_user_id
            foreign key (user_id) references users(id) on delete restrict;
    end if;
    if not exists (select 1 from pg_constraint where conname = 'ck_payments_amounts_coherent') then
        alter table payments add constraint ck_payments_amounts_coherent check (
            subtotal_amount >= 0
            and service_fee_amount >= 0
            and platform_commission_amount >= 0
            and host_net_amount >= 0
            and amount = subtotal_amount + service_fee_amount
            and host_net_amount = subtotal_amount - platform_commission_amount
            and (refunded_amount is null or refunded_amount > 0)
        );
    end if;
end $$;

create unique index if not exists ux_payments_provider_payment_id
    on payments (provider, provider_payment_id) where provider_payment_id is not null;

create unique index if not exists ux_payments_user_idempotency
    on payments (user_id, idempotency_key) where idempotency_key <> '';

-- Una reserva solo conserva un pago vigente; los rechazados permiten un nuevo intento.
create unique index if not exists ux_payments_active_reservation
    on payments (reservation_id) where status in ('Pending', 'Paid');

create index if not exists ix_payments_status on payments (status);

create table if not exists payment_gateway_attempts (
    id bigint generated always as identity primary key,
    payment_id integer not null references payments(id) on delete cascade,
    provider varchar(40) not null,
    provider_reference_id varchar(120) null,
    outcome varchar(40) not null,
    failure_code varchar(80) null,
    created_at timestamptz not null default now(),
    constraint ck_payment_gateway_attempts_outcome check (
        outcome in ('Created', 'Approved', 'Rejected', 'Refunded')
    )
);

create index if not exists ix_payment_gateway_attempts_payment
    on payment_gateway_attempts (payment_id, created_at);

create table if not exists payment_webhook_events (
    id bigint generated always as identity primary key,
    provider varchar(40) not null,
    provider_event_id varchar(120) not null,
    payment_id integer not null references payments(id) on delete cascade,
    event_type varchar(40) not null,
    created_at timestamptz not null default now()
);

create unique index if not exists ux_payment_webhook_events_provider_event
    on payment_webhook_events (provider, provider_event_id);

create table if not exists refunds (
    id bigint generated always as identity primary key,
    payment_id integer not null references payments(id) on delete restrict,
    amount numeric(10,2) not null,
    reason varchar(500) null,
    status varchar(40) not null default 'Completed',
    provider varchar(40) not null,
    provider_refund_id varchar(120) null,
    requested_by_user_id integer not null references users(id) on delete restrict,
    created_at timestamptz not null default now(),
    constraint ck_refunds_amount_positive check (amount > 0),
    constraint ck_refunds_status check (status in ('Completed'))
);

create unique index if not exists ux_refunds_payment_id on refunds (payment_id);
