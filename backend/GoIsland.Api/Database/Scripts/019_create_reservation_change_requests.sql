create table if not exists reservation_change_requests (
    id integer generated always as identity primary key,
    reservation_id integer not null references reservations(id) on delete cascade,
    requested_by_user_id integer not null references users(id) on delete restrict,
    type varchar(20) not null,
    status varchar(20) not null default 'Pending',
    reason varchar(500) not null,
    requested_schedule_id integer null references experience_schedules(id) on delete restrict,
    reviewed_by_user_id integer null references users(id) on delete restrict,
    reviewed_at timestamptz null,
    decision_reason varchar(500) null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint ck_reservation_change_requests_type check (type in ('Cancel', 'Reschedule')),
    constraint ck_reservation_change_requests_status check (status in ('Pending', 'Approved', 'Rejected'))
);

create index if not exists ix_reservation_change_requests_reservation_id
    on reservation_change_requests (reservation_id);

create unique index if not exists ux_reservation_change_requests_one_pending
    on reservation_change_requests (reservation_id)
    where status = 'Pending';
