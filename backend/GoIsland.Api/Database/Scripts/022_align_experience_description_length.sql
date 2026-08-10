-- Alinea el contrato público de experiencias con el tamaño admitido por PostgreSQL.

alter table experiences
    alter column description type varchar(4000);
