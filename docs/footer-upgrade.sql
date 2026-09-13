CREATE TABLE IF NOT EXISTS "FooterEntries" (
 "Id" uuid PRIMARY KEY,"ParentId" uuid NULL REFERENCES "FooterEntries"("Id") ON DELETE RESTRICT,
 "Name" varchar(100) NOT NULL,"Kind" varchar(20) NOT NULL,"Text" varchar(4000) NOT NULL,
 "Url" varchar(2048) NOT NULL,"Icon" varchar(30) NOT NULL,"SortOrder" integer NOT NULL DEFAULT 0,
 "OpenNewTab" boolean NOT NULL DEFAULT false,"IsArchived" boolean NOT NULL DEFAULT false,
 "IsDeleted" boolean NOT NULL DEFAULT false,"Version" integer NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS "IX_FooterEntries_ParentId" ON "FooterEntries"("ParentId");
CREATE TABLE IF NOT EXISTS "FooterRevisions" (
 "Id" uuid PRIMARY KEY,"EntryId" uuid NOT NULL REFERENCES "FooterEntries"("Id") ON DELETE RESTRICT,
 "Version" integer NOT NULL,"Action" text NOT NULL,"Actor" text NOT NULL,"Snapshot" text NOT NULL,"CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_FooterRevisions_EntryId_Version" ON "FooterRevisions"("EntryId","Version");
INSERT INTO "FooterEntries"("Id","ParentId","Name","Kind","Text","Url","Icon")
SELECT '00000000-0000-0000-0000-000000001301',NULL,'General','section','','',''
WHERE NOT EXISTS(SELECT 1 FROM "FooterEntries");
INSERT INTO "FooterEntries"("Id","ParentId","Name","Kind","Text","Url","Icon")
SELECT '00000000-0000-0000-0000-000000001302','00000000-0000-0000-0000-000000001301','Copyright','copyright','© '||chr(123)||'year'||chr(125)||' - PaladinHub | Made with 💛 for WoW Paladins','',''
WHERE EXISTS(SELECT 1 FROM "FooterEntries" WHERE "Id"='00000000-0000-0000-0000-000000001301') AND NOT EXISTS(SELECT 1 FROM "FooterRevisions")
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "FooterRevisions"("Id","EntryId","Version","Action","Actor","Snapshot","CreatedAtUtc")
SELECT gen_random_uuid(),e."Id",e."Version",'imported','system',row_to_json(e)::text,NOW() FROM "FooterEntries" e WHERE NOT EXISTS(SELECT 1 FROM "FooterRevisions" r WHERE r."EntryId"=e."Id");
