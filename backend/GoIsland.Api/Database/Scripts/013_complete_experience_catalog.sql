alter table experiences
    add column if not exists slug varchar(180),
    add column if not exists short_description varchar(300) not null default '',
    add column if not exists duration_minutes integer,
    add column if not exists time_zone_id varchar(80) not null default 'America/Santo_Domingo',
    add column if not exists meeting_point_instructions varchar(1000) not null default '',
    add column if not exists pickup_information varchar(1000),
    add column if not exists what_is_included text[] not null default '{}',
    add column if not exists what_is_not_included text[] not null default '{}',
    add column if not exists what_to_bring text[] not null default '{}',
    add column if not exists guest_requirements varchar(1500) not null default '',
    add column if not exists minimum_age integer,
    add column if not exists difficulty varchar(40) not null default '',
    add column if not exists accessibility_information varchar(1500) not null default '',
    add column if not exists languages text[] not null default '{}',
    add column if not exists cancellation_policy varchar(40) not null default '',
    add column if not exists tags text[] not null default '{}';

update experiences
set slug = lower(regexp_replace(trim(title), '[^a-zA-Z0-9]+', '-', 'g')) || '-' || id
where slug is null or slug = '';

alter table experiences alter column slug set not null;
create unique index if not exists ux_experiences_slug on experiences(slug);

create table if not exists experience_itinerary_items (
    id integer generated always as identity primary key,
    experience_id integer not null references experiences(id) on delete cascade,
    title varchar(120) not null,
    description varchar(800) not null,
    duration_minutes integer not null,
    location varchar(160),
    sort_order integer not null,
    constraint ck_itinerary_duration check (duration_minutes between 1 and 1440),
    constraint ck_itinerary_sort_order check (sort_order >= 0)
);

create unique index if not exists ux_itinerary_experience_sort
    on experience_itinerary_items(experience_id, sort_order);
