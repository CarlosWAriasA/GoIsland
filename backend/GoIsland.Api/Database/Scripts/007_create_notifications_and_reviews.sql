create table if not exists outbox_messages (
    id bigserial primary key,
    user_id integer not null references users(id) on delete cascade,
    reservation_id integer null references reservations(id) on delete cascade,
    type varchar(80) not null,
    title varchar(160) not null,
    message varchar(1000) not null,
    action_url varchar(500),
    status varchar(40) not null default 'Pending',
    attempt_count integer not null default 0,
    next_attempt_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    processed_at timestamptz,
    last_error varchar(500),
    constraint ck_outbox_status check (status in ('Pending', 'Processing', 'Processed', 'Failed'))
);
create index if not exists ix_outbox_ready on outbox_messages(status, next_attempt_at);

create table if not exists notifications (
    id serial primary key,
    user_id integer not null references users(id) on delete cascade,
    outbox_message_id bigint not null references outbox_messages(id) on delete cascade,
    type varchar(80) not null,
    title varchar(160) not null,
    message varchar(1000) not null,
    action_url varchar(500),
    read_at timestamptz,
    created_at timestamptz not null default now(),
    constraint uq_notifications_outbox unique(outbox_message_id)
);
create index if not exists ix_notifications_user_created on notifications(user_id, created_at desc);

create table if not exists user_notification_preferences (
    user_id integer primary key references users(id) on delete cascade,
    dashboard_enabled boolean not null default true,
    email_enabled boolean not null default true,
    push_enabled boolean not null default true,
    updated_at timestamptz not null default now()
);

create table if not exists device_tokens (
    id serial primary key,
    user_id integer not null references users(id) on delete cascade,
    token varchar(4096) not null,
    platform varchar(20) not null,
    created_at timestamptz not null default now(),
    last_seen_at timestamptz not null default now(),
    constraint uq_device_token unique(token),
    constraint ck_device_platform check (platform in ('Web', 'Android', 'iOS'))
);
create index if not exists ix_device_tokens_user on device_tokens(user_id);

create table if not exists outbox_attempts (
    id bigserial primary key,
    outbox_message_id bigint not null references outbox_messages(id) on delete cascade,
    channel varchar(40) not null,
    succeeded boolean not null,
    error_code varchar(120),
    created_at timestamptz not null default now()
);
create index if not exists ix_outbox_attempts_message_channel on outbox_attempts(outbox_message_id, channel);

create table if not exists capacity_audits (
    id bigserial primary key,
    schedule_id integer not null references experience_schedules(id) on delete cascade,
    reservation_id integer null references reservations(id) on delete set null,
    previous_spots integer not null,
    new_spots integer not null,
    reason varchar(120) not null,
    created_at timestamptz not null default now(),
    constraint ck_capacity_audit_values check (previous_spots >= 0 and new_spots >= 0)
);

create table if not exists reviews (
    id serial primary key,
    reservation_id integer not null references reservations(id) on delete restrict,
    user_id integer not null references users(id) on delete restrict,
    experience_id integer not null references experiences(id) on delete restrict,
    host_id integer not null references users(id) on delete restrict,
    rating integer not null,
    comment varchar(1000) not null,
    moderation_status varchar(40) not null default 'Visible',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint uq_reviews_reservation unique(reservation_id),
    constraint ck_review_rating check (rating between 1 and 5),
    constraint ck_review_status check (moderation_status in ('Visible', 'Hidden', 'Deleted', 'Reported'))
);
create index if not exists ix_reviews_experience_status on reviews(experience_id, moderation_status);
create index if not exists ix_reviews_host_status on reviews(host_id, moderation_status);
