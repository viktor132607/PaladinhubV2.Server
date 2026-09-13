CREATE TABLE IF NOT EXISTS "SeoEntries" (
    "Id" uuid PRIMARY KEY,
    "PageId" integer NULL,
    "Path" varchar(2048) NOT NULL DEFAULT '*',
    "Title" varchar(200) NOT NULL DEFAULT '',
    "Description" varchar(500) NOT NULL DEFAULT '',
    "CanonicalUrl" varchar(2048) NOT NULL DEFAULT '',
    "SocialTitle" varchar(200) NOT NULL DEFAULT '',
    "SocialDescription" varchar(500) NOT NULL DEFAULT '',
    "SocialImageMediaId" uuid NULL,
    "ImageUrl" varchar(2048) NOT NULL DEFAULT '',
    "Index" boolean NULL,
    "Follow" boolean NULL,
    "IsArchived" boolean NOT NULL DEFAULT false,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "Version" integer NOT NULL DEFAULT 1
);

ALTER TABLE "SeoEntries"
    ADD COLUMN IF NOT EXISTS "SocialImageMediaId" uuid NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint constraint_row
        JOIN pg_attribute column_row
          ON column_row.attrelid = constraint_row.conrelid
         AND column_row.attnum = ANY(constraint_row.conkey)
        WHERE constraint_row.contype = 'f'
          AND constraint_row.conrelid = '"SeoEntries"'::regclass
          AND constraint_row.confrelid = '"ContentPages"'::regclass
          AND column_row.attname = 'PageId'
    ) THEN
        ALTER TABLE "SeoEntries"
            ADD CONSTRAINT "FK_SeoEntries_ContentPages_PageId"
            FOREIGN KEY ("PageId")
            REFERENCES "ContentPages"("Id")
            ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint constraint_row
        JOIN pg_attribute column_row
          ON column_row.attrelid = constraint_row.conrelid
         AND column_row.attnum = ANY(constraint_row.conkey)
        WHERE constraint_row.contype = 'f'
          AND constraint_row.conrelid = '"SeoEntries"'::regclass
          AND constraint_row.confrelid = '"SpellIcons"'::regclass
          AND column_row.attname = 'SocialImageMediaId'
    ) THEN
        ALTER TABLE "SeoEntries"
            ADD CONSTRAINT "FK_SeoEntries_SpellIcons_SocialImageMediaId"
            FOREIGN KEY ("SocialImageMediaId")
            REFERENCES "SpellIcons"("Id")
            ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_SeoEntries_PageId"
    ON "SeoEntries"("PageId");
CREATE INDEX IF NOT EXISTS "IX_SeoEntries_SocialImageMediaId"
    ON "SeoEntries"("SocialImageMediaId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_SeoEntries_Active_PageId"
    ON "SeoEntries"("PageId")
    WHERE "PageId" IS NOT NULL AND NOT "IsDeleted";
CREATE UNIQUE INDEX IF NOT EXISTS "UX_SeoEntries_Active_StaticPath"
    ON "SeoEntries"(lower("Path"))
    WHERE "PageId" IS NULL AND NOT "IsDeleted";

CREATE TABLE IF NOT EXISTS "SeoRevisions" (
    "Id" uuid PRIMARY KEY,
    "EntryId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "Action" varchar(30) NOT NULL,
    "Actor" varchar(256) NOT NULL,
    "Snapshot" text NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint constraint_row
        JOIN pg_attribute column_row
          ON column_row.attrelid = constraint_row.conrelid
         AND column_row.attnum = ANY(constraint_row.conkey)
        WHERE constraint_row.contype = 'f'
          AND constraint_row.conrelid = '"SeoRevisions"'::regclass
          AND constraint_row.confrelid = '"SeoEntries"'::regclass
          AND column_row.attname = 'EntryId'
    ) THEN
        ALTER TABLE "SeoRevisions"
            ADD CONSTRAINT "FK_SeoRevisions_SeoEntries_EntryId"
            FOREIGN KEY ("EntryId")
            REFERENCES "SeoEntries"("Id")
            ON DELETE RESTRICT;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SeoRevisions_EntryId_Version"
    ON "SeoRevisions"("EntryId", "Version");

CREATE OR REPLACE FUNCTION "PreventSeoMediaDeactivation"()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF (NEW."IsDeleted" OR NEW."IsArchived")
       AND (NEW."IsDeleted" IS DISTINCT FROM OLD."IsDeleted"
            OR NEW."IsArchived" IS DISTINCT FROM OLD."IsArchived")
       AND EXISTS (
           SELECT 1
           FROM "SeoEntries" seo
           WHERE NOT seo."IsDeleted"
             AND seo."SocialImageMediaId" = NEW."Id"
       ) THEN
        RAISE EXCEPTION 'Media is referenced by SEO settings.'
            USING ERRCODE = '23503';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS "TR_SpellIcons_PreventSeoMediaDeactivation"
    ON "SpellIcons";
CREATE TRIGGER "TR_SpellIcons_PreventSeoMediaDeactivation"
BEFORE UPDATE OF "IsDeleted", "IsArchived" ON "SpellIcons"
FOR EACH ROW
EXECUTE FUNCTION "PreventSeoMediaDeactivation"();
