create table if not exists web_push_subscriptions (
    id serial primary key,
    user_id integer not null references users(id) on delete cascade,
    endpoint varchar(4096) not null,
    p256dh varchar(512) not null,
    auth varchar(512) not null,
    expiration_time timestamptz,
    created_at timestamptz not null default now(),
    last_seen_at timestamptz not null default now(),
    constraint uq_web_push_subscription_endpoint unique(endpoint)
);
create index if not exists ix_web_push_subscriptions_user
    on web_push_subscriptions(user_id);

-- Los tokens FCM no se pueden convertir a suscripciones Web Push. Al retirar Firebase,
-- los navegadores deben conceder permiso y registrarse una vez con el nuevo contrato VAPID.
drop table if exists device_tokens;
