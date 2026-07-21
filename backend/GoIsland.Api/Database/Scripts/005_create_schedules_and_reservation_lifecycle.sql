create table if not exists experience_schedules (
    id integer generated always as identity primary key,
    experience_id integer not null references experiences(id) on delete restrict,
    starts_at timestamptz not null,
    ends_at timestamptz not null,
    capacity integer not null,
    available_spots integer not null,
    status varchar(40) not null default 'Scheduled',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint ck_experience_schedules_dates check (ends_at > starts_at),
    constraint ck_experience_schedules_capacity_positive check (capacity > 0),
    constraint ck_experience_schedules_available_non_negative check (available_spots >= 0),
    constraint ck_experience_schedules_available_capacity check (available_spots <= capacity),
    constraint ck_experience_schedules_status check (
        status in ('Scheduled', 'Closed', 'Cancelled', 'Completed')
    )
);

create index if not exists ix_experience_schedules_experience_starts
    on experience_schedules (experience_id, starts_at);

alter table reservations add column if not exists schedule_id integer null;
alter table reservations add column if not exists updated_at timestamptz null;
alter table reservations add column if not exists cancelled_at timestamptz null;

-- Las reservas anteriores quedan vinculadas a un horario histórico cerrado. No se publica como
-- disponibilidad futura ni permite reservas nuevas.
insert into experience_schedules (
    experience_id, starts_at, ends_at, capacity, available_spots, status, created_at, updated_at
)
select
    reservation_group.experience_id,
    reservation_group.first_reservation,
    reservation_group.first_reservation + interval '1 hour',
    greatest(experience.capacity, reservation_group.reserved_spots),
    0,
    'Completed',
    reservation_group.first_reservation,
    now()
from (
    select
        experience_id,
        min(reservation_date) as first_reservation,
        sum(quantity)::integer as reserved_spots
    from reservations
    where schedule_id is null
    group by experience_id
) reservation_group
join experiences experience on experience.id = reservation_group.experience_id;

update reservations reservation
set schedule_id = (
    select candidate.id
    from experience_schedules candidate
    where candidate.experience_id = reservation.experience_id
      and candidate.status = 'Completed'
    order by candidate.id desc
    limit 1
)
where reservation.schedule_id is null;

alter table reservations drop constraint if exists ck_reservations_status;

update reservations set status = 'PendingPayment' where status = 'Pending';
update reservations set status = 'CancelledByTourist', cancelled_at = reservation_date
where status = 'Cancelled';
update reservations set updated_at = reservation_date where updated_at is null;

alter table reservations alter column schedule_id set not null;
alter table reservations alter column updated_at set not null;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'ck_reservations_lifecycle_status') then
        alter table reservations add constraint ck_reservations_lifecycle_status check (
            status in (
                'PendingPayment', 'Confirmed', 'CancelledByTourist', 'CancelledByHost',
                'Completed', 'RefundPending', 'Refunded'
            )
        );
    end if;
    if not exists (select 1 from pg_constraint where conname = 'fk_reservations_schedule_id') then
        alter table reservations add constraint fk_reservations_schedule_id
            foreign key (schedule_id) references experience_schedules(id) on delete restrict;
    end if;
end $$;

create index if not exists ix_reservations_schedule_id on reservations (schedule_id);

create table if not exists reservation_status_history (
    id bigint generated always as identity primary key,
    reservation_id integer not null references reservations(id) on delete cascade,
    from_status varchar(40) null,
    to_status varchar(40) not null,
    changed_by_user_id integer not null references users(id) on delete restrict,
    reason varchar(500) null,
    created_at timestamptz not null default now()
);

create index if not exists ix_reservation_status_history_reservation_created
    on reservation_status_history (reservation_id, created_at);

insert into reservation_status_history (
    reservation_id, from_status, to_status, changed_by_user_id, reason, created_at
)
select reservation.id, null, reservation.status, reservation.user_id,
       'Estado importado durante la migración de calendario.', reservation.reservation_date
from reservations reservation
where not exists (
    select 1 from reservation_status_history history
    where history.reservation_id = reservation.id
);

create table if not exists reservation_idempotency_keys (
    id bigint generated always as identity primary key,
    user_id integer not null references users(id) on delete restrict,
    operation varchar(80) not null,
    idempotency_key varchar(100) not null,
    request_hash varchar(64) not null,
    reservation_id integer not null references reservations(id) on delete cascade,
    created_at timestamptz not null default now()
);

create unique index if not exists ux_reservation_idempotency_keys_scope
    on reservation_idempotency_keys (user_id, operation, idempotency_key);
