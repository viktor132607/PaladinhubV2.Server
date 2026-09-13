# Point 15.6 — Roles & Permissions final security audit

Status: final verification candidate. Do not mark point 15 complete until this branch and the matching Client branch are published by safe fast-forward and both `main` push CI runs are green.

## Audited architecture

- ASP.NET Identity remains the only user/role membership system (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`).
- `RoleSecurityProfiles`, `RolePermissions`, `RoleSecurityRevisions` and `AccessControlAuditEntries` extend Identity; they do not duplicate it.
- the protected system administrator role remains `Admin` and receives the complete current effective catalog;
- the authoritative permission catalog contains 143 permission IDs after the final orphan cleanup, including resource-level `manage` umbrella grants;
- `AdminEndpointRegistry` remains the source-of-truth mapping for administrative controller/action/HTTP-method permissions, including administrative routes outside `/Admin` and explicitly mixed-access actions;
- every non-mixed registry row is exercised by the enforcement regression gate, while legacy Admin-authorized actions are reflection-checked for registry coverage;
- `talent_trees.read` is the one operational read permission with a deliberate Client-only purpose: it gates the Talent Tree Builder route while the public guide consumes the same read data.

## Security findings closed by 15.6

The final audit found a real self-escalation gap that earlier stages did not close: a delegated user with `user_roles.update` could target their own user ID, and a user with role-permission administration could add permissions or restore/re-enable a more privileged state on a custom role they already held.

`AccessControlMutationGuard` now blocks:

- assigning any role to yourself, including the system Admin role;
- adding permissions to a role that is already assigned to the acting user;
- re-enabling a disabled role that is already assigned to the acting user;
- restoring a role revision that would add permissions or re-enable a role assigned to the acting user.

Privilege reduction remains allowed, and an administrator who is not a member of the target custom role can continue to manage it normally. Existing Admin self-demotion and last-active-administrator protections remain in the transactional service layer.

## Permission-catalog audit

Nine operation IDs created during the broad 15.1 vocabulary pass had no actual endpoint or Client behavior and were retired instead of being left as assignable no-op grants:

- `users.create`, `users.update`, `users.delete`;
- `talent_pages.delete`;
- `media.create`, `media.archive`;
- `spell_icons.delete`;
- `product_reviews.read`;
- `promo_codes.delete`.

The PostgreSQL upgrade deletes stale grants for these retired IDs idempotently. A PostgreSQL integration test applies the upgrade repeatedly to an old-style Identity schema, verifies retired grants are removed, valid grants remain, and existing user-role memberships are unchanged. A catalog-purpose regression test fails if a future non-`manage` permission has neither an endpoint mapping nor an explicit Client-only purpose.

## Attack and invariant coverage

The point-15 server test suite now covers the following access-control attack/invariant cases:

- anonymous administrative requests => 401;
- authenticated ordinary user without grants => 403;
- read-only and editor personas receive only their current granular grants;
- protected Admin receives the full effective system permission set;
- unknown permission IDs are rejected server-side;
- role names are case-insensitively unique;
- stale role versions are rejected with conflict;
- the protected Admin role cannot be renamed, disabled, re-permissioned to a reduced set or deleted;
- roles with assigned users cannot be silently deleted/reassigned;
- administrators cannot remove their own Admin membership;
- concurrent last-Admin revocation leaves exactly one active Admin membership;
- role membership assignment/revocation rotates the Identity security stamp;
- live permission and membership revocation changes authorization for an already authenticated cookie on the next protected request;
- stale cookies are rejected after the configured security-stamp validation interval;
- self-role assignment and own-role privilege expansion/re-enable/privileged revision restore are blocked;
- missing and invalid antiforgery tokens on authenticated access-control mutations return HTTP 400 and do not create the requested role;
- PostgreSQL migration is repeatable, preserves existing Admin and custom role memberships, and keeps role-security foreign-key invariants;
- assignment/delete concurrent mutation coverage verifies that an assignment and deletion of the same role cannot both succeed or leave a partial role/membership state.

## Administrative regression coverage

The existing full suite and catalog checks continue to cover the administrative feature set introduced before and during point 15, including categories/classes-specializations/tags/patches/rarities/record types/spell icons/media/navigation, Page Builder pages/blocks/history/templates/presets/Talent Tree Builder, localization, banners, footer, SEO, carts/store/products/review moderation/promo codes, and access-control management itself.

## Verification history

- 15.5 published baseline: Server `ef8ff28f99961dabd46d3c76db92a121d41d4da5`, Client `82d986d71efd6a9483a92d327e6adea2f8319c4d`.
- point-15.6 self-escalation branch gate: Server CI #115, 736/736 tests, PostgreSQL 17, Release build 0 errors.
- point-15.6 catalog/old-database cleanup branch gate: Server CI #119, 738/738 tests, PostgreSQL 17, Release build 0 errors.
- later final attack/concurrency commits must pass a fresh Server CI before publication; the final result is recorded only after the `main` push CI is green.

Known pre-existing Server warnings remain visible and are not claimed as fixed by point 15: NU1903 advisories for `Microsoft.OpenApi` 2.0.0 and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, plus existing nullable/analyzer, EF1002 and ASPDEPR005 warnings.

GitHub source publication and CI are not evidence that Render has completed a production deployment.
