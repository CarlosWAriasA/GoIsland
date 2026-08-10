-- Separa la administracion del rol de participacion: hasta ahora 'Admin' ocupaba la columna
-- role, asi que un administrador no podia ser turista ni anfitrion al mismo tiempo.

alter table users
    add column if not exists is_admin boolean not null default false;

-- La cuenta que sostiene el contenido sin dueño no administra nada: solo necesita ser anfitriona
-- para no dejar huerfanas sus experiencias.
update users
set role = 'Host'
where role = 'Admin'
  and email = 'legacy-content@goisland.invalid';

-- Quien administraba conserva el permiso y pasa a anfitrion si ya publicaba experiencias, para
-- no dejar huerfano el contenido que le pertenece.
update users
set is_admin = true,
    role = case
        when exists (select 1 from experiences where experiences.host_id = users.id) then 'Host'
        else 'Tourist'
    end
where role = 'Admin';

alter table users
    drop constraint if exists ck_users_role;

alter table users
    add constraint ck_users_role check (role in ('Tourist', 'Host'));
