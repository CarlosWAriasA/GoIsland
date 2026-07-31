alter table experience_images
    add column if not exists provider varchar(40) not null default 'Local',
    add column if not exists public_id varchar(255),
    add column if not exists secure_url varchar(1000),
    add column if not exists width integer,
    add column if not exists height integer,
    add column if not exists format varchar(20),
    add column if not exists alt_text varchar(180) not null default '',
    add column if not exists is_cover boolean not null default false;

update experience_images image
set is_cover = true
where image.id = (
    select candidate.id
    from experience_images candidate
    where candidate.experience_id = image.experience_id
    order by candidate.sort_order, candidate.id
    limit 1
)
and not exists (
    select 1
    from experience_images existing_cover
    where existing_cover.experience_id = image.experience_id
      and existing_cover.is_cover
);

create unique index if not exists ux_experience_images_public_id
    on experience_images(public_id)
    where public_id is not null;

create unique index if not exists ux_experience_images_one_cover
    on experience_images(experience_id)
    where is_cover;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_experience_images_dimensions'
    ) then
        alter table experience_images
            add constraint ck_experience_images_dimensions
            check (
                (width is null and height is null)
                or (width > 0 and height > 0)
            );
    end if;

    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_experience_images_cloudinary_metadata'
    ) then
        alter table experience_images
            add constraint ck_experience_images_cloudinary_metadata
            check (
                provider <> 'Cloudinary'
                or (
                    public_id is not null
                    and secure_url is not null
                    and width is not null
                    and height is not null
                    and format is not null
                )
            );
    end if;
end
$$;
