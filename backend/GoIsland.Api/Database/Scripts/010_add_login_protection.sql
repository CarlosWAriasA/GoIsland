alter table users
    add column if not exists failed_login_attempts integer not null default 0;

alter table users
    add column if not exists lockout_end timestamptz null;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_users_failed_login_attempts_non_negative'
    ) then
        alter table users
            add constraint ck_users_failed_login_attempts_non_negative
            check (failed_login_attempts >= 0);
    end if;
end $$;
