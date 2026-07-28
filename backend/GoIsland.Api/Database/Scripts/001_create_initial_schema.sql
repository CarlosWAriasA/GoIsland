create table if not exists users (
    id integer generated always as identity primary key,
    full_name varchar(120) not null,
    email varchar(254) not null,
    password_hash text not null,
    role varchar(40) not null default 'Tourist',
    failed_login_attempts integer not null default 0,
    lockout_end timestamptz null,
    created_at timestamptz not null default now(),
    constraint ck_users_role check (role in ('Tourist', 'Host', 'Admin')),
    constraint ck_users_failed_login_attempts_non_negative check (failed_login_attempts >= 0)
);

create unique index if not exists ux_users_email_lower
    on users (lower(email));

create table if not exists experiences (
    id integer generated always as identity primary key,
    title varchar(160) not null,
    description varchar(2000) not null,
    location varchar(160) not null,
    category varchar(80) not null,
    price numeric(10, 2) not null,
    capacity integer not null,
    available_spots integer not null,
    is_approved boolean not null default false,
    created_at timestamptz not null default now(),
    constraint ck_experiences_price_non_negative check (price >= 0),
    constraint ck_experiences_capacity_non_negative check (capacity >= 0),
    constraint ck_experiences_available_spots_non_negative check (available_spots >= 0),
    constraint ck_experiences_available_spots_capacity check (available_spots <= capacity)
);

create table if not exists reservations (
    id integer generated always as identity primary key,
    user_id integer not null references users(id) on delete restrict,
    experience_id integer not null references experiences(id) on delete restrict,
    quantity integer not null,
    status varchar(40) not null default 'Pending',
    total_amount numeric(10, 2) not null,
    reservation_date timestamptz not null default now(),
    constraint ck_reservations_quantity_positive check (quantity > 0),
    constraint ck_reservations_total_amount_non_negative check (total_amount >= 0),
    constraint ck_reservations_status check (status in ('Pending', 'Confirmed', 'Cancelled'))
);

create index if not exists ix_reservations_user_id
    on reservations (user_id);

create index if not exists ix_reservations_experience_id
    on reservations (experience_id);

create table if not exists payments (
    id integer generated always as identity primary key,
    reservation_id integer not null references reservations(id) on delete restrict,
    amount numeric(10, 2) not null,
    status varchar(40) not null default 'Pending',
    created_at timestamptz not null default now(),
    constraint ck_payments_amount_non_negative check (amount >= 0),
    constraint ck_payments_status check (status in ('Pending', 'Paid', 'Failed', 'Refunded'))
);

create index if not exists ix_payments_reservation_id
    on payments (reservation_id);
