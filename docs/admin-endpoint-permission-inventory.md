# Administrative endpoint and permission inventory

Point 15 uses one authorization model end to end. ASP.NET Identity remains the source of users, roles and memberships; the access-control tables add granular grants, protected-role metadata, revisions and audit records rather than creating a second identity system.

## Source of truth

- `PaladinHubV2.Server.Core/Security/AdminPermissions.cs` defines stable lowercase permission IDs by resource and operation.
- `PaladinHubV2.Server.API/Security/AdminEndpointRegistry.cs` maps the administrative HTTP surface to those permissions, including admin capabilities outside `/Admin` and mixed public/owner actions.
- `PaladinHubV2.Server.Core/Security/SystemRoleCatalog.cs` defines protected system-role semantics.
- `EffectivePermissionService` resolves current database memberships and grants. Disabled roles do not grant access; a resource `manage` grant satisfies the narrower supported operations for that resource; the protected `Admin` role receives the complete declared catalog.
- `AdminPermissionEnforcementMiddleware` performs the registry-driven permission decision after authentication and before MVC authorization.

The executable registry tests remain authoritative for catalog uniqueness, known permission IDs, endpoint/action coverage and granular enforcement. The route prefix itself is never treated as an authorization rule.

## Permission resources

The catalog covers users, roles, role permissions, user-role assignments, Page Builder pages/blocks/templates/presets, talent pages/trees, navigation, banners, footer, SEO, localization, database browsing, media, categories, classes/specializations, tags, patches, rarities, record types, spell icons, spells, items, archived carts/orders, products, review moderation and promo codes.

Representative IDs are `pages.read`, `pages.update`, `seo.restore`, `role_permissions.update` and `user_roles.manage`. Unknown IDs are rejected instead of silently stored or normalized.

## Persistence and protected roles

The existing Identity role `Admin` is the protected bootstrap administrator role. The additive access-control model uses:

- `RoleSecurityProfiles` for protected/disabled state and optimistic versioning;
- `RolePermissions` for granular role grants;
- `RoleSecurityRevisions` for restorable role snapshots;
- `AccessControlAuditEntries` for durable actor/target/old/new mutation records.

`AspNetUsers`, `AspNetRoles` and `AspNetUserRoles` remain the real Identity tables. Role and membership mutations use those records directly and rotate the affected user's Identity security stamp.

System-role protections include reserved/protected name semantics, no disable/delete/reduced permission set for `Admin`, self-demotion protection and PostgreSQL-serialized last-active-administrator protection.

## Management API

`/Admin/api/access-control` provides:

- `GET /permissions` — `role_permissions.read`;
- `GET /roles` and `GET /roles/{roleId}` — `roles.read`;
- `POST /roles` — `roles.create`;
- `PUT /roles/{roleId}` — `roles.update`;
- `DELETE /roles/{roleId}?version=...` — `roles.delete`;
- `PUT /roles/{roleId}/permissions` — `role_permissions.update`;
- `GET /roles/{roleId}/history` — `roles.read`;
- `POST /roles/{roleId}/restore` — `roles.restore`;
- `GET /roles/{roleId}/users` — `user_roles.read`;
- `GET /users` — `users.read`;
- `POST` / `DELETE /roles/{roleId}/users/{userId}` — `user_roles.update`;
- `GET /audit` — `roles.read`.

Mutations retain automatic antiforgery validation, optimistic concurrency and the access-control invariants above.

## Runtime enforcement

Authorization is based on current database state on every granular permission decision, not a stale permission list embedded in the authentication cookie. Successful granular authorization supplies only a request-local `Admin` compatibility claim so legacy `Authorize(Roles = "Admin")` attributes remain a secondary MVC boundary without persisting elevation into the cookie.

Lifecycle endpoints resolve the required `archive`, `delete` or `restore` permission from the posted action and rewind the request body before MVC binding. Mixed-access endpoints are not blanket locked: public/owner behavior remains available, while administrative overrides call the permission evaluator.

Administrative routes outside `/Admin` are inventoried as well, including presets, talent-tree mutation, cart archive views and product mutation routes. Page Builder block render helpers require `page_blocks.read`; preset and talent mutations use antiforgery validation; the legacy destructive product GET was converted to HTTP 405; hidden-product visibility and review moderation use granular permissions instead of direct Admin-role checks.

## Real HTTP proof

Point 15.4 uses `WebApplicationFactory`, real Identity cookie authentication, antiforgery and PostgreSQL-backed Identity/access-control state. It proves anonymous 401, authenticated-without-grant 403, read-only/editor custom-role access, protected Admin effective rights, live grant replacement, live membership revocation, security-stamp rotation and eventual rejection of a stale cookie by the real Identity validator.

Only the test session cache is memory-backed so the authorization test is not coupled to a developer-specific distributed-cache endpoint.

## Client effective permissions — 15.5

`/api/auth/me` now returns the authenticated user's current effective permission IDs together with Identity roles. The client uses that set for presentation and navigation only; the server remains authoritative for every request.

The client stage provides:

- permission-aware `/Admin` entry instead of a hardcoded `Admin` role check;
- a reusable `PermissionRoute` with login redirect and `/Error/403` handling;
- granular guards for every mapped admin route, including non-`/Admin` product/cart routes;
- permission-filtered admin navigation and Page Builder tabs;
- responsive `/Admin/Roles` and `/Admin/Users` workspaces;
- role CRUD, permission matrix, history/restore, audit and membership management without exposing operations the session cannot perform;
- permission-aware create/update/archive/delete/restore controls across the principal lifecycle CRUD screens;
- read-only rendering for users that have only the resource `read` permission;
- dependency-aware Database UI so it does not issue unauthorized auxiliary reads for categories/classes/tags/patches/rarities;
- current-session refresh after changing the signed-in user's membership.

The client does not infer authority from role names. Its effective permission set is refreshed from the server session endpoint, while the API independently resolves current membership/grants on every protected request. Therefore hidden/disabled client controls are usability boundaries, not security boundaries.

## Point 15 completion boundary

15.1 completed the catalog, endpoint inventory and persistence foundation. 15.2 completed management APIs, audit/history and transactional administrator safety. 15.3 completed centralized granular runtime enforcement and endpoint hardening. 15.4 proved real HTTP authorization and active-session revocation. 15.5 completes effective-permission session projection and the Roles/Users/route/menu/action client layer.

Point 15 is considered publishable only after the final Server and Client branch CI gates are green and both branches are confirmed ahead of, and not behind, their current `main` branches. Publication to GitHub `main` is source-control evidence only and is not proof of a Render deployment.
