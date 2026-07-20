create table if not exists host_profiles (
    id integer generated always as identity primary key,
    user_id integer not null references users(id) on delete restrict,
    display_name varchar(120) not null,
    description varchar(1000) not null,
    phone_number varchar(30) not null,
    verification_status varchar(40) not null default 'Pending',
    rejection_reason varchar(500) null,
    submitted_at timestamptz not null default now(),
    reviewed_at timestamptz null,
    reviewed_by_admin_id integer null references users(id) on delete restrict,
    constraint ck_host_profiles_status check (
        verification_status in ('Pending', 'Approved', 'Rejected', 'Suspended')
    )
);

create unique index if not exists ux_host_profiles_user_id on host_profiles (user_id);
create index if not exists ix_host_profiles_status on host_profiles (verification_status);

create table if not exists admin_audit_logs (
    id bigint generated always as identity primary key,
    admin_user_id integer not null references users(id) on delete restrict,
    entity_type varchar(80) not null,
    entity_id integer not null,
    action varchar(80) not null,
    reason varchar(500) null,
    created_at timestamptz not null default now()
);

create index if not exists ix_admin_audit_logs_entity
    on admin_audit_logs (entity_type, entity_id);

alter table experiences add column if not exists host_id integer null;
alter table experiences add column if not exists approval_status varchar(40) null;
alter table experiences add column if not exists rejection_reason varchar(500) null;
alter table experiences add column if not exists reviewed_at timestamptz null;
alter table experiences add column if not exists reviewed_by_admin_id integer null;
alter table experiences add column if not exists updated_at timestamptz null;

do $$
declare
    legacy_owner_id integer;
begin
    select id into legacy_owner_id
    from users
    where role in ('Host', 'Admin')
    order by case when role = 'Admin' then 0 else 1 end, id
    limit 1;

    if legacy_owner_id is null and exists (select 1 from experiences where host_id is null) then
        insert into users (full_name, email, password_hash, role)
        values (
            'Contenido legado GoIsland',
            'legacy-content@goisland.invalid',
            'ACCOUNT_LOCKED_NO_LOGIN',
            'Admin'
        )
        on conflict do nothing;

        select id into legacy_owner_id
        from users
        where email = 'legacy-content@goisland.invalid';
    end if;

    update experiences
    set host_id = legacy_owner_id
    where host_id is null;
end $$;

update experiences
set approval_status = case when is_approved then 'Approved' else 'PendingReview' end
where approval_status is null;

update experiences set updated_at = created_at where updated_at is null;

alter table experiences alter column host_id set not null;
alter table experiences alter column approval_status set not null;
alter table experiences alter column updated_at set not null;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'fk_experiences_host_id') then
        alter table experiences
            add constraint fk_experiences_host_id
            foreign key (host_id) references users(id) on delete restrict;
    end if;
    if not exists (select 1 from pg_constraint where conname = 'fk_experiences_reviewed_by_admin_id') then
        alter table experiences
            add constraint fk_experiences_reviewed_by_admin_id
            foreign key (reviewed_by_admin_id) references users(id) on delete restrict;
    end if;
    if not exists (select 1 from pg_constraint where conname = 'ck_experiences_approval_status') then
        alter table experiences
            add constraint ck_experiences_approval_status check (
                approval_status in ('Draft', 'PendingReview', 'Approved', 'Rejected', 'Suspended')
            );
    end if;
end $$;

create index if not exists ix_experiences_host_id on experiences (host_id);
create index if not exists ix_experiences_approval_status on experiences (approval_status);

-- Los roles Host concedidos por el registro publico anterior no equivalen a una
-- verificacion administrativa. Solo se conserva el rol si ya existe un perfil aprobado.
update users
set role = 'Tourist'
where role = 'Host'
  and not exists (
      select 1
      from host_profiles
      where host_profiles.user_id = users.id
        and host_profiles.verification_status = 'Approved'
  );
