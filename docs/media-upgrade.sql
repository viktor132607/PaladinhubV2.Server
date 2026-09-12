BEGIN;
ALTER TABLE "SpellIcons" ADD COLUMN IF NOT EXISTS "AltText" varchar(500) NOT NULL DEFAULT '';
ALTER TABLE "SpellIcons" ADD COLUMN IF NOT EXISTS "Description" varchar(2000) NOT NULL DEFAULT '';
ALTER TABLE "SpellIcons" ADD COLUMN IF NOT EXISTS "IsArchived" boolean NOT NULL DEFAULT false;
ALTER TABLE "SpellIcons" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT false;
ALTER TABLE "SpellIcons" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;
CREATE TABLE IF NOT EXISTS "MediaRevisions" (
 "Id" uuid PRIMARY KEY, "MediaId" uuid NOT NULL REFERENCES "SpellIcons"("Id") ON DELETE RESTRICT,
 "Version" integer NOT NULL, "Action" varchar(30) NOT NULL, "Actor" varchar(256) NOT NULL,
 "Snapshot" text NOT NULL, "CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MediaRevisions_MediaId_Version" ON "MediaRevisions" ("MediaId", "Version");
INSERT INTO "MediaRevisions" ("Id", "MediaId", "Version", "Action", "Actor", "Snapshot", "CreatedAtUtc")
SELECT gen_random_uuid(), i."Id", i."Version", 'imported', 'database-upgrade',
 json_build_object('Name',i."Name",'AltText',i."AltText",'Description',i."Description",'IsArchived',i."IsArchived",'IsDeleted',i."IsDeleted")::text, now()
FROM "SpellIcons" i WHERE NOT EXISTS (SELECT 1 FROM "MediaRevisions" r WHERE r."MediaId"=i."Id");
ALTER TABLE "Items" ALTER COLUMN "Icon" TYPE varchar(2048);
ALTER TABLE "Items" ALTER COLUMN "SecondIcon" TYPE varchar(2048);
COMMIT;
