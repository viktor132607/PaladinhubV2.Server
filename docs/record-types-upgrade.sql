-- Run after spell-icons-upgrade.sql if startup migrations are disabled.
BEGIN;
DO $$
BEGIN
    IF to_regclass('"RecordTypes"') IS NULL THEN
        CREATE TABLE "RecordTypes" ("Name" character varying(50) PRIMARY KEY);
        INSERT INTO "RecordTypes" ("Name") VALUES ('item'), ('spell'), ('talent');
        UPDATE "Spells" SET "Quality" = COALESCE(NULLIF(lower(trim("Quality")), ''), 'spell');
        INSERT INTO "RecordTypes" ("Name") SELECT DISTINCT "Quality" FROM "Spells" ON CONFLICT DO NOTHING;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Spells_RecordTypes_Quality' AND conrelid = '"Spells"'::regclass AND confupdtype = 'c') THEN
        ALTER TABLE "Spells" DROP CONSTRAINT IF EXISTS "FK_Spells_RecordTypes_Quality";
        ALTER TABLE "Spells" ADD CONSTRAINT "FK_Spells_RecordTypes_Quality"
            FOREIGN KEY ("Quality") REFERENCES "RecordTypes" ("Name") ON UPDATE CASCADE ON DELETE RESTRICT;
    END IF;
END $$;
COMMIT;
