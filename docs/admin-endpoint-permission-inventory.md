# Administrative endpoint and permission inventory

Point 15.1 establishes the source-of-truth catalogs used by later enforcement work:

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
- products, review moderation and promo codes.

Permissions use stable lowercase IDs such as `pages.read`, `pages.update`, `seo.restore` and `user_roles.manage`. Unknown permission IDs are rejected by `PermissionCatalogValidator`; they are never silently accepted or case-normalized.

## Current system role

The existing ASP.NET Identity role `Admin` is the protected administrator role. Point 15 does not introduce a second user/role system and does not rename existing production roles. `Admin` has protected name/deletion/permission-set semantics and its effective required permission set is the complete current catalog.

The database foundation is additive to the existing Identity tables:

- `RoleSecurityProfiles` stores protected/disabled state and an optimistic-concurrency version;
- `RolePermissions` stores granular role grants;
- `RoleSecurityRevisions` stores versioned audit snapshots for the role-management stage.

Existing `AspNetUsers`, `AspNetRoles` and `AspNetUserRoles` remain Identity-owned.

## Non-`/Admin` administrative routes found during the audit

The route prefix cannot be used as an authorization inventory rule. Current examples include:

- `/api/presets` — Admin-only preset CRUD/preview;
- `/api/talents/{key}` — Admin-only talent-tree mutation;
- `/api/products` and `/Products` mutation aliases — Admin-only product create/edit/delete paths;
- public product detail paths with an in-action Admin visibility override;
- authenticated review deletion with an in-action Admin moderation override.

These are explicit registry entries so later permission enforcement cannot omit them.

## Security debt intentionally recorded for 15.3

15.1 inventories these issues but does not change endpoint behavior yet:

- Page Builder preview helpers `/api/blocks/render` and `/api/blocks/render-layout` are currently unauthenticated;
- preset POST/PUT/DELETE actions currently lack antiforgery validation;
- `POST /api/talents/{key}` currently lacks antiforgery validation;
- legacy `GET Products/DeleteProduct` performs a destructive product delete;
- product visibility and review moderation contain direct `User.IsInRole("Admin")` checks instead of centralized permission authorization.

The enforcement stage must remove or harden these paths rather than preserving them as exceptions.

## Stage boundary

15.1 is the inventory/catalog/database foundation only. It does **not** claim that granular permissions are enforced yet. Role CRUD, user-role mutations and last-admin transactional business rules are 15.2; replacing legacy Admin-role gates with permission policies and adding the unmapped-endpoint enforcement guard is 15.3.
