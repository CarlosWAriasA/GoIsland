BEGIN;

CREATE EXTENSION IF NOT EXISTS unaccent;

-- unaccent() se declara STABLE porque, sin argumentos, depende del diccionario de búsqueda
-- activo. Al fijar el diccionario explícitamente el resultado es determinista, así que se
-- envuelve en una función IMMUTABLE para poder indexar la expresión.
CREATE OR REPLACE FUNCTION goisland_normalize(value text)
    RETURNS text
    LANGUAGE sql
    IMMUTABLE
    PARALLEL SAFE
    RETURNS NULL ON NULL INPUT
AS $$
    SELECT lower(public.unaccent('public.unaccent', value))
$$;

-- Índices trigram sobre el texto normalizado. Reemplazan a los que operaban sobre la
-- columna cruda, que ya no participan en las búsquedas.
DROP INDEX IF EXISTS ix_experiences_title_trgm;
DROP INDEX IF EXISTS ix_experiences_summary_trgm;
DROP INDEX IF EXISTS ix_experiences_location_trgm;

CREATE INDEX IF NOT EXISTS ix_experiences_title_norm_trgm
    ON experiences USING gin (goisland_normalize(title) gin_trgm_ops)
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_summary_norm_trgm
    ON experiences USING gin (goisland_normalize(short_description) gin_trgm_ops)
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_location_norm_trgm
    ON experiences USING gin (goisland_normalize(location) gin_trgm_ops)
    WHERE approval_status = 'Approved';

-- Los filtros por categoría y dificultad comparan el valor completo ya normalizado.
DROP INDEX IF EXISTS ix_experiences_public_category;
DROP INDEX IF EXISTS ix_experiences_public_difficulty;

CREATE INDEX IF NOT EXISTS ix_experiences_public_category_norm
    ON experiences (goisland_normalize(category))
    WHERE approval_status = 'Approved';

CREATE INDEX IF NOT EXISTS ix_experiences_public_difficulty_norm
    ON experiences (goisland_normalize(difficulty))
    WHERE approval_status = 'Approved';

COMMIT;
