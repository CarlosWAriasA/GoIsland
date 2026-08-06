alter table experience_images
    add column if not exists credit_text varchar(240) not null default '',
    add column if not exists credit_url varchar(1000),
    add column if not exists license_name varchar(80),
    add column if not exists license_url varchar(1000);
