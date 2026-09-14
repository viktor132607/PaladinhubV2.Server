-- PaladinHub V2 point 15 access-control foundation.
-- This upgrade is intentionally additive: ASP.NET Identity keeps ownership of
-- AspNetUsers/AspNetRoles/AspNetUserRoles and existing assignments are not
-- rewritten. Run it after the identity/user seeder so the Admin role exists.

CREATE TABLE IF NOT EXISTS "RoleSecurityProfiles" (
    "RoleId" text PRIMARY KEY,
    "IsSystem" boolean NOT NULL DEFAULT false,
    "IsDisabled" boolean NOT NULL DEFAULT false,
    "Version" integer NOT NULL DEFAULT 1,
    "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "CK_RoleSecurityProfiles_Version" CHECK ("Version" > 0),
    CONSTRAINT "FK_RoleSecurityProfiles_AspNetRoles_RoleId"
        FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "RolePermissions" (
    "RoleId" text NOT NULL,
    "PermissionId" character varying(128) NOT NULL,
    "GrantedBy" character varying(256) NOT NULL DEFAULT 'migration',
    "GrantedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("RoleId", "PermissionId"),
    CONSTRAINT "FK_RolePermissions_RoleSecurityProfiles_RoleId"
        FOREIGN KEY ("RoleId") REFERENCES "RoleSecurityProfiles" ("RoleId") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "RoleSecurityRevisions" (
    "Id" uuid PRIMARY KEY,
    "RoleId" text NOT NULL,
    "Version" integer NOT NULL,
    "Action" character varying(30) NOT NULL,
    "Actor" character varying(256) NOT NULL,
    "Snapshot" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "CK_RoleSecurityRevisions_Version" CHECK ("Version" > 0),
    CONSTRAINT "FK_RoleSecurityRevisions_RoleSecurityProfiles_RoleId"
        FOREIGN KEY ("RoleId") REFERENCES "RoleSecurityProfiles" ("RoleId") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "AccessControlAuditEntries" (
    "Id" uuid PRIMARY KEY,
    "Action" character varying(40) NOT NULL,
    "ActorId" character varying(450) NOT NULL,
    "Actor" character varying(256) NOT NULL,
    "TargetRoleId" character varying(450) NULL,
    "TargetRoleName" character varying(256) NULL,
    "TargetUserId" character varying(450) NULL,
    "TargetUserName" character varying(256) NULL,
    "OldState" text NOT NULL DEFAULT '{{}}',
    "NewState" text NOT NULL DEFAULT '{{}}',
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_RolePermissions_PermissionId"
    ON "RolePermissions" ("PermissionId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_RoleSecurityRevisions_RoleId_Version"
    ON "RoleSecurityRevisions" ("RoleId", "Version");

CREATE INDEX IF NOT EXISTS "IX_AccessControlAuditEntries_TargetRoleId_CreatedAtUtc"
    ON "AccessControlAuditEntries" ("TargetRoleId", "CreatedAtUtc");

CREATE INDEX IF NOT EXISTS "IX_AccessControlAuditEntries_TargetUserId_CreatedAtUtc"
    ON "AccessControlAuditEntries" ("TargetUserId", "CreatedAtUtc");

-- Create security profiles for every role that already exists. This preserves
-- all existing ASP.NET Identity role membership rows exactly as they are.
INSERT INTO "RoleSecurityProfiles" (
    "RoleId",
    "IsSystem",
    "IsDisabled",
    "Version",
    "UpdatedAtUtc")
SELECT
    role."Id",
    lower(role."Name") = 'admin',
    false,
    1,
    CURRENT_TIMESTAMP
FROM "AspNetRoles" role
ON CONFLICT ("RoleId") DO NOTHING;

-- The legacy Admin role is the protected system administrator role. Never let
-- an old profile leave it disabled after upgrading an existing database.
UPDATE "RoleSecurityProfiles" profile
SET
    "IsSystem" = true,
    "IsDisabled" = false,
    "UpdatedAtUtc" = CURRENT_TIMESTAMP
FROM "AspNetRoles" role
WHERE profile."RoleId" = role."Id"
  AND lower(role."Name") = 'admin'
  AND (profile."IsSystem" = false OR profile."IsDisabled" = true);

-- Point 15.6 retired operation IDs that never mapped to a real administrative
-- capability. Remove stale grants idempotently so old databases cannot keep
-- no-op permissions that are no longer part of the authoritative catalog.
DELETE FROM "RolePermissions"
WHERE "PermissionId" IN (
    'users.create',
    'users.update',
    'users.delete',
    'talent_pages.delete',
    'media.create',
    'media.archive',
    'spell_icons.delete',
    'product_reviews.read',
    'promo_codes.delete'
);
