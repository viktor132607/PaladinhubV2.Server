CREATE TABLE IF NOT EXISTS "SeoEntries"(
 "Id" uuid PRIMARY KEY,"PageId" integer NULL REFERENCES "ContentPages"("Id") ON DELETE RESTRICT,
 "Path" varchar(2048) NOT NULL,"Title" varchar(200) NOT NULL,"Description" varchar(500) NOT NULL,
 "CanonicalUrl" varchar(2048) NOT NULL,"SocialTitle" varchar(200) NOT NULL,"SocialDescription" varchar(500) NOT NULL,
 "ImageUrl" varchar(2048) NOT NULL,"Index" boolean NULL,"Follow" boolean NULL,
 "IsArchived" boolean NOT NULL DEFAULT false,"IsDeleted" boolean NOT NULL DEFAULT false,"Version" integer NOT NULL DEFAULT 1);
CREATE INDEX IF NOT EXISTS "IX_SeoEntries_PageId" ON "SeoEntries"("PageId");
CREATE TABLE IF NOT EXISTS "SeoRevisions"("Id" uuid PRIMARY KEY,"EntryId" uuid NOT NULL REFERENCES "SeoEntries"("Id") ON DELETE RESTRICT,"Version" integer NOT NULL,"Action" text NOT NULL,"Actor" text NOT NULL,"Snapshot" text NOT NULL,"CreatedAtUtc" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SeoRevisions_EntryId_Version" ON "SeoRevisions"("EntryId","Version");
