# Administrative endpoint and permission inventory

Point 15 establishes source-of-truth catalogs for granular administration:

- `PaladinHubV2.Server.Core/Security/AdminPermissions.cs` — stable permission IDs grouped by resource and operation;
- `PaladinHubV2.Server.API/Security/AdminEndpointRegistry.cs` — current administrative action inventory with HTTP method, route, controller/action, current protection and target permission;
- `PaladinHubV2.Server.Core/Security/SystemRoleCatalog.cs` — protected system-role semantics.

The executable tests are authoritative for catalog uniqueness and for coverage of every controller action currently protected by the legacy `Admin` role. The inventory deliberately also records administrative capabilities that are not discoverable from `/Admin` route prefixes. Point 15.3 adds an exhaustive middleware test that resolves and permission-checks every non-mixed registry row, so registry entries are not merely documentation.

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

`/Admin/api/access-control` is the role/user management API introduced in point 15.2. Point 15.3 now puts these actions through the same centralized granular permission enforcement as the broader administrative surface. Automatic antiforgery validation remains enabled for mutation requests.

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

## Runtime enforcement in 15.3/15.4

`AdminPermissionEnforcementMiddleware` is placed after authentication and before ASP.NET authorization. It resolves the current `ControllerActionDescriptor` against `AdminEndpointRegistry`, obtains the exact permission for the HTTP method/action, and evaluates that permission against the user's current database role memberships and grants.

Important properties:

- authorization uses the current `AspNetUserRoles`, `RoleSecurityProfiles` and `RolePermissions` state on each permission decision rather than trusting a permission snapshot embedded in an authentication cookie;
- disabled role profiles do not grant permissions;
- a resource `manage` grant satisfies a supported narrower operation on that same resource;
- protected system roles are evaluated against `SystemRoleCatalog.RequiredPermissions`, so the protected `Admin` role receives the complete declared effective permission set even when those permissions are not materialized as individual `RolePermissions` rows;
- lifecycle endpoints select `archive`, `delete` or `restore` permission from the posted action and rewind the request body before MVC model binding;
- successful granular authorization supplies only a request-local `Admin` role claim so existing legacy `Authorize(Roles = "Admin")` attributes remain a secondary boundary without persisting elevation to the Identity cookie;
- mixed-access endpoints are deliberately not blanket-locked. Their elevated branch uses `IAdminPermissionEvaluator` while their public/owner behavior remains available under its original rules.

The exhaustive enforcement regression test iterates every non-mixed registry row. If a registered action cannot be resolved by controller/action/HTTP method, or if it bypasses the permission decision, the test fails.

## Non-`/Admin` administrative routes

The route prefix is not an authorization rule. Current examples include:

- `/api/presets` — permission-enforced preset CRUD/preview; mutation methods now use automatic antiforgery validation;
- `/api/talents/{key}` — permission-enforced talent-tree mutation with automatic antiforgery validation;
- `/api/cart/archive` and legacy cart aliases — permission-enforced archived cart/order views;
- `/api/products` and `/Products` mutation aliases — permission-enforced product create/edit/delete paths;
- public product detail paths whose hidden-state override now requires `products.read` rather than a direct Admin-role check;
- authenticated review deletion whose moderation override now requires `product_reviews.delete` rather than a direct Admin-role check;
- Page Builder block render/preview helpers, now protected by the registry-driven permission middleware even though they do not use a legacy Admin attribute.

## 15.3 security-debt closure

The security debt recorded in 15.1/15.2 is closed in this stage:

- `/api/blocks/render` and `/api/blocks/render-layout` no longer execute anonymously; `page_blocks.read` is required;
- preset POST/PUT/DELETE actions now use antiforgery validation;
- `POST /api/talents/{key}` now uses antiforgery validation;
- legacy `GET Products/DeleteProduct` is non-destructive and returns HTTP 405; product deletion remains on the antiforgery-protected DELETE endpoint;
- product hidden-state visibility and review moderation no longer call `User.IsInRole("Admin")`; they use granular permission evaluation.

## 15.4 HTTP and session proof

Point 15.4 executes the actual ASP.NET Core application through `WebApplicationFactory` with real Identity cookie authentication, real antiforgery login flow and a real PostgreSQL access-control database. It proves:

- anonymous administrative access returns 401, including the Page Builder render helper protected by the permission middleware;
- an ordinary authenticated user without grants returns 403;
- a read-only custom role is allowed only its declared permission;
- an editor custom role is allowed both permissions assigned to it;
- the protected `Admin` system role is authorized from its declared system-role effective permission set even with zero physical grant rows;
- replacing a custom role's permissions takes effect on the next request for an already-authenticated cookie;
- revoking a user's role membership takes effect on the next protected request for that same cookie;
- membership mutation changes the user's Identity security stamp;
- after the security-stamp validation interval elapses, the pre-existing cookie is rejected by the real Identity validator.

The HTTP test keeps Identity users/roles and permission state on PostgreSQL. Only the test session cache is replaced with an in-memory implementation so the authorization test is not coupled to a developer-specific distributed-cache endpoint.

## Stage boundary

15.1 completed inventory/catalog/database foundation. 15.2 completed role CRUD, permission replacement, membership management, audit/history/restore and transactional administrator safety. 15.3 completed centralized runtime granular enforcement and endpoint hardening. 15.4 now proves authorization and revocation semantics through real authenticated HTTP sessions.

Point 15.5 remains the Roles/Users client UI and effective-permission route/menu/action gating. Point 15 as a whole is therefore still not complete or published to `main`.
