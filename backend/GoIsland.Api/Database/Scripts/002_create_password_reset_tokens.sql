create table if not exists password_reset_tokens (
    id integer generated always as identity primary key,
    user_id integer not null references users(id) on delete cascade,
    token_hash varchar(64) not null,
    expires_at timestamptz not null,
    used_at timestamptz null,
    created_at timestamptz not null default now()
);

create unique index if not exists ux_password_reset_tokens_token_hash
    on password_reset_tokens (token_hash);

create index if not exists ix_password_reset_tokens_user_id
    on password_reset_tokens (user_id);

create index if not exists ix_password_reset_tokens_active
    on password_reset_tokens (user_id, expires_at)
    where used_at is null;
