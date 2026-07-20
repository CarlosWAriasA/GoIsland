CREATE TABLE IF NOT EXISTS user_external_logins (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    provider VARCHAR(40) NOT NULL,
    provider_subject VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_user_external_logins_provider_subject
    ON user_external_logins(provider, provider_subject);

CREATE UNIQUE INDEX IF NOT EXISTS ux_user_external_logins_user_provider
    ON user_external_logins(user_id, provider);
