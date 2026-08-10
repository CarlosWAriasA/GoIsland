-- Separa la visibilidad del estado de moderacion: hasta ahora una experiencia aprobada solo
-- salia del catalogo si un administrador la suspendia. El anfitrion necesita poder retirarla
-- por su cuenta sin perder el historial ni pasar otra vez por revision.

alter table experiences
    add column if not exists is_hidden boolean not null default false;

-- El catalogo publico filtra por esta columna en cada consulta, asi que conviene tenerla en el
-- indice que ya sostiene el listado aprobado.
create index if not exists ix_experiences_visibility
    on experiences (approval_status, is_hidden)
    where is_approved;
