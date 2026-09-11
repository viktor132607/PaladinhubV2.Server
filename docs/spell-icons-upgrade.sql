-- Applied automatically when APPLY_MIGRATIONS_ON_STARTUP=true.
-- Use this script for an existing database when startup upgrades are disabled.
BEGIN;
CREATE TABLE IF NOT EXISTS "SpellIcons" (
    "Id" uuid PRIMARY KEY,
    "Name" character varying(255) NOT NULL,
    "ContentType" character varying(32) NOT NULL,
    "Content" bytea NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
ALTER TABLE "Spells" ALTER COLUMN "Icon" TYPE character varying(2048);
COMMIT;
