BEGIN;
ALTER TABLE "ContentPages" ADD COLUMN IF NOT EXISTS "IsArchived" boolean NOT NULL DEFAULT false;
ALTER TABLE "ContentPages" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT false;
ALTER TABLE "ContentPages" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;
CREATE TABLE IF NOT EXISTS "PageRevisions" (
 "Id" uuid PRIMARY KEY, "PageId" integer NOT NULL REFERENCES "ContentPages"("Id") ON DELETE RESTRICT,
 "Version" integer NOT NULL, "Action" varchar(30) NOT NULL, "Actor" varchar(256) NOT NULL,
 "Snapshot" text NOT NULL, "CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PageRevisions_PageId_Version" ON "PageRevisions" ("PageId", "Version");
INSERT INTO "PageRevisions" ("Id","PageId","Version","Action","Actor","Snapshot","CreatedAtUtc")
SELECT gen_random_uuid(), p."Id",p."Version",'imported','database-upgrade',
 json_build_object('Section',p."Section",'Slug',p."Slug",'Title',p."Title",'IsPublished',p."IsPublished",'JsonLayout',p."JsonLayout",'IsArchived',p."IsArchived",'IsDeleted',p."IsDeleted")::text,now()
FROM "ContentPages" p WHERE NOT EXISTS (SELECT 1 FROM "PageRevisions" r WHERE r."PageId"=p."Id");
COMMIT;
