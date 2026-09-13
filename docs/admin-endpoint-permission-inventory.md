# Administrative endpoint and permission inventory

Point 15 establishes source-of-truth catalogs for granular administration:

- `PaladinHubV2.Server.Core/Security/AdminPermissions.cs` — stable permission IDs grouped by resource and operation;
- `PaladinHubV2.Server.API/Security/AdminEndpointRegistry.cs` — current administrative action inventory with HTTP method, route, controller/action, current protection and target permission;
- `PaladinHubV2.Server.Core/Security/SystemRoleCatalog.cs` — protected system-role semantics.

The executable tests are authoritative for catalog uniqueness and for coverage of every controller action currently protected by the legacy `Admin` role. The inventory deliberately also records administrative capabilities that are not discoverable from `/Admin` route prefixes.

## Resources

The permission catalog currently covers:

- users, roles, role permissions and user-role assignments;
- Page Builder pages, page blocks, templates and data presets;
- dynamic talent pages and talent-tree administration;
- navigation, banners, footer, SEO and localization;
- administrative database browsing and media;
- categories, classes/specializations, tags, patches, rarities and record types;
- spell icons, spells and items;
- archived carts/orders, products, review moderation and promo codes.

Permissions use stable lowercase IDs such as `pages.read`, `pages.update`, `seo.restore` and `user_roles.manage`. Unknown permission IDs are rejected by `PermissionCatalogValidator`; they are never silently accepted or case-normalized.

## Current system role

The existing ASP.NET Identity role `Admin` is the protected administrator role. Point 15 does not introduce a second user/role system and does not rename existing production roles. `Admin` has protected name/deletion/permission-set semantics and its effective required permission set is the complete current catalog.

The database foundation is additive to the existing Identity tables:

- `RoleSecurityProfiles` stores protected/disabled state and an optimistic-concurrency version;
- `RolePermissions` stores granular role grants;
- `RoleSecurityRevisions` stores versioned role snapshots;
- `AccessControlAuditEntries` stores durable actor/target/old/new state for role and membership mutations.

Existing `AspNetUsers`, `AspNetRoles` and `AspNetUserRoles` remain the actual Identity tables. The access-control management context maps those same tables so role metadata and membership changes can share one transaction boundary; it does not create parallel user or role records.

## 15.2 management API

`/Admin/api/access-control` is the role/user management API introduced in point 15.2. It remains behind the legacy `Admin` authorization boundary until point 15.3 applies granular policies, and automatic antiforgery validation remains enabled for mutation requests.

Current operations are explicitly registered:

- `GET /permissions` — permission catalog (`role_permissions.read`);
- `GET /roles` and `GET /roles/{roleId}` — role listing/details (`roles.read`);
- `POST /roles` — create a role (`roles.create`);
- `PUT /roles/{roleId}` — rename/disable supported custom roles (`roles.update`);
- `DELETE /roles/{roleId}?version=...` — delete an unused custom role (`roles.delete`);
- `PUT /roles/{roleId}/permissions` — atomically replace validated grants (`role_permissions.update`);
- `GET /roles/{roleId}/history` and `POST /roles/{roleId}/restore` — version history and invariant-checked restore (`roles.read` / `roles.restore`);
- `GET /roles/{roleId}/users` — assigned users (`user_roles.read`);
- `GET /users` — bounded searchable user list with current roles (`users.read`);
- `POST`/`DELETE /roles/{roleId}/users/{userId}` — explicit assignment/revocation (`user_roles.update`);
- `GET /audit` — durable access-control audit stream (`roles.read`).

Management invariants include trimmed/case-insensitive role-name uniqueness, unknown-permission rejection, optimistic role versions, protected system-role name/disable/delete/permission-set rules, no implicit reassignment when deleting an in-use role, security-stamp rotation on membership changes, self-demotion protection and transaction-serialized last-active-administrator protection.

## Non-`/Admin` administrative routes found during the audit

The route prefix cannot be used as an authorization inventory rule. Current examples include:

- `/api/presets` — Admin-only preset CRUD/preview;
- `/api/talents/{key}` — Admin-only talent-tree mutation;
- `/api/cart/archive` and legacy cart aliases — Admin-only archived cart/order views;
- `/api/products` and `/Products` mutation aliases — Admin-only product create/edit/delete paths;
- public product detail paths with an in-action Admin visibility override;
- authenticated review deletion with an in-action Admin moderation override.

These are explicit registry entries so later permission enforcement cannot omit them.

## Security debt intentionally recorded for 15.3

15.1/15.2 inventory these issues but do not change their endpoint behavior yet:

- Page Builder preview helpers `/api/blocks/render` and `/api/blocks/render-layout` are currently unauthenticated;
- preset POST/PUT/DELETE actions currently lack antiforgery validation;
- `POST /api/talents/{key}` currently lacks antiforgery validation;
- legacy `GET Products/DeleteProduct` performs a destructive product delete;
- product visibility and review moderation contain direct `User.IsInRole("Admin")` checks instead of centralized permission authorization.

The enforcement stage must remove or harden these paths rather than preserving them as exceptions.

## Stage boundary

15.1 completed the inventory/catalog/database foundation. 15.2 adds the role CRUD, validated permission replacement, user-role assignment/revocation, audit/history/restore and transactional administrator-safety business layer. It still does **not** claim granular permission enforcement on the broader admin surface.

Point 15.3 must replace the legacy Admin-role gates with centralized permission policies/handlers and keep an automatic guard that rejects newly added administrative actions without registry/permission mapping. Point 15.4 then proves active-session revocation and authorization behavior through the real HTTP pipeline.
