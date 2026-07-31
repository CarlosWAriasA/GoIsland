alter table experiences
    add column if not exists is_unlimited_capacity boolean not null default false;

create table if not exists experience_images (
    id integer generated always as identity primary key,
    experience_id integer not null references experiences(id) on delete cascade,
    file_name varchar(120) not null,
    content_type varchar(80) not null,
    sort_order integer not null,
    created_at timestamptz not null default now(),
    constraint ck_experience_images_sort_order check (sort_order >= 0)
);

create unique index if not exists ux_experience_images_experience_sort
    on experience_images(experience_id, sort_order);

create unique index if not exists ux_experience_images_file_name
    on experience_images(file_name);
