-- Point 12. Idempotent for empty and existing databases.
CREATE TABLE IF NOT EXISTS "SiteBanners"(
 "Id" uuid PRIMARY KEY,"InternalName" varchar(100) NOT NULL,"Title" varchar(200) NOT NULL,"Text" text NOT NULL,
 "ImageUrl" varchar(2048),"AltText" varchar(300) NOT NULL DEFAULT '',"ButtonText" varchar(120),"ButtonUrl" varchar(2048),
 "Kind" varchar(16) NOT NULL,"Position" varchar(32) NOT NULL,"PagesJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
 "StartAtUtc" timestamptz,"EndAtUtc" timestamptz,"SortOrder" integer NOT NULL DEFAULT 0,"IsDismissible" boolean NOT NULL DEFAULT true,
 "IsActive" boolean NOT NULL DEFAULT true,"IsArchived" boolean NOT NULL DEFAULT false,"IsDeleted" boolean NOT NULL DEFAULT false,
 "Version" integer NOT NULL DEFAULT 1,"CreatedAtUtc" timestamptz NOT NULL,"UpdatedAtUtc" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SiteBanners_InternalName" ON "SiteBanners"(lower("InternalName")) WHERE NOT "IsDeleted";
CREATE INDEX IF NOT EXISTS "IX_SiteBanners_Visibility" ON "SiteBanners"("IsDeleted","IsArchived","IsActive","StartAtUtc","EndAtUtc","Position","SortOrder");
CREATE TABLE IF NOT EXISTS "BannerRevisions"(
 "Id" uuid PRIMARY KEY,"BannerId" uuid NOT NULL REFERENCES "SiteBanners"("Id") ON DELETE RESTRICT,"Version" integer NOT NULL,
 "Action" varchar(32) NOT NULL,"Actor" varchar(256) NOT NULL,"CreatedAtUtc" timestamptz NOT NULL,"Snapshot" jsonb NOT NULL,
 CONSTRAINT "UQ_BannerRevisions_Banner_Version" UNIQUE("BannerId","Version"));
DO $$ BEGIN
 IF to_regclass('"SpellIcons"') IS NOT NULL THEN
  EXECUTE 'CREATE OR REPLACE FUNCTION ph_protect_banner_media() RETURNS trigger AS $f$ BEGIN IF NEW."IsDeleted" AND NOT OLD."IsDeleted" AND EXISTS (SELECT 1 FROM "SiteBanners" b WHERE NOT b."IsDeleted" AND b."ImageUrl" IS NOT NULL AND b."ImageUrl" ILIKE ''%'' || OLD."Id"::text || ''%'') THEN RAISE EXCEPTION ''media_in_use_banner'' USING ERRCODE = ''23503''; END IF; RETURN NEW; END; $f$ LANGUAGE plpgsql';
  DROP TRIGGER IF EXISTS "TR_SpellIcons_ProtectBannerMedia" ON "SpellIcons";
  CREATE TRIGGER "TR_SpellIcons_ProtectBannerMedia" BEFORE UPDATE OF "IsDeleted" ON "SpellIcons" FOR EACH ROW EXECUTE FUNCTION ph_protect_banner_media();
 END IF;
END $$;
