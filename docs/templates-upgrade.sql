CREATE TABLE IF NOT EXISTS "ContentTemplates" (
 "Id" uuid PRIMARY KEY, "Name" varchar(100) NOT NULL, "Description" varchar(1000) NOT NULL,
 "Kind" varchar(30) NOT NULL, "JsonLayout" text NOT NULL, "IsArchived" boolean NOT NULL DEFAULT false,
 "IsDeleted" boolean NOT NULL DEFAULT false, "Version" integer NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS "ContentTemplateRevisions" (
 "Id" uuid PRIMARY KEY, "TemplateId" uuid NOT NULL REFERENCES "ContentTemplates"("Id") ON DELETE RESTRICT,
 "Version" integer NOT NULL, "Action" varchar(30) NOT NULL, "Actor" varchar(100) NOT NULL,
 "Snapshot" text NOT NULL, "CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ContentTemplateRevisions_TemplateId_Version" ON "ContentTemplateRevisions"("TemplateId", "Version");
