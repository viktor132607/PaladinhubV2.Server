DROP INDEX IF EXISTS "IX_Carts_UserId";

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Carts_UserId_Active"
    ON "Carts" ("UserId")
    WHERE "IsArchived" = FALSE;
