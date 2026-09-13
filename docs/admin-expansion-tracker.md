# PaladinHub V2 admin expansion tracker

Baseline after point 11: `43de1b4f300d6fdc38e2e954ff7ac138ff231293`.

## Point 12 — banners and messages

Status: implementation candidate; complete only after CI and remote-main verification.

Implemented:
- versioned banner records with UTC scheduling, page scope, ordering, dismissibility, archive and soft-delete states;
- admin list/create/update/archive/unarchive/delete/history/revision restore endpoints;
- public endpoint filters deleted, archived, inactive, future, expired and page-mismatched rows;
- stable localization keys based on banner ID;
- URL scheme validation blocks `javascript:` and other unsupported schemes;
- stale-version mutations return HTTP 409;
- revision history stores actor, timestamp, action and full restorable snapshot;
- database trigger prevents soft-deleting media referenced by a non-deleted banner;
- `docs/banners-upgrade.sql` is idempotent; API schema bootstrap uses the same idempotent DDL.

Checks required before completion:
- server CI restore/build/tests;
- SQL create + second execution against PostgreSQL test database where available;
- endpoint boundary checks (`start == now`, `end == now`), page scope, ordering, archive/delete/restore and stale version;
- client CI/tests/static production export and responsive checks at 428×926, 926×428 and desktop;
- exact diff / whitespace check;
- re-fetch `origin/main` and verify no foreign commits were overwritten.

Limitations / evidence policy:
- CI is build/test evidence, not production-deployment evidence.
- responsive browser emulation is not a physical-iPhone test.
- no mock API run is recorded as backend integration evidence.

Commit: recorded after the verified point-12 commit is published to `main`.

## Review after the incomplete banner implementation

Found and corrected: API requests were executing DDL; banner data logic lived in the controller; deleted banners could bypass revision recovery through unarchive; unsafe backslash URLs were accepted; media usage was absent from admin counts; editing schedule dates shifted local time; image browsing loaded only the first media page; the static banner route was missing; below-navbar banners could overlap the fixed navbar. Restored readable baseline layouts while preserving every existing route and the banner additions.

Verification: server/API build; 9 banner validation/controller/boundary tests; client static build and 17 tests; actual upgrade SQL executed twice in PGlite with media deletion-trigger checks; mocked-browser UTC/local-time round trip, CSRF/version submission, dismissal/new-version display and 428×926/926×428/1440×926 dimensions. Public request DDL removed; schema now uses the existing startup migration gate. Production deployment and full PostgreSQL/Npgsql CRUD integration are not claimed.

Remaining sequence: 13 footer, 14 SEO, 15 roles/permissions, complete translation inventory/coverage, then unit tests for all remaining controllers. These are not marked complete.

## Point 13 — footer and contacts

Implemented versioned entries with section FKs, typed links and contacts, ordering/moving, archive and revision restore. Section deletion is blocked while entries remain. Added responsive admin editor, public data rendering, intentional-empty vs failure fallback, copyright year replacement, and stable translation keys. Default V1/V2 copyright is seeded once. Verification: client/server builds, 11 focused tests, actual SQL idempotency/preservation/FK checks in PGlite. Browser checks passed: section/contact CRUD, history restore, typed mailto links, empty-response versus outage fallback, and portrait/landscape/desktop dimensions. Explicit form-label associations were corrected after the browser checks caught ambiguous labels.

Banner fixes published: Server `d66019fa7ebc73f689b2eaa9bc6baaab2c1d2acb`; Client `527684ff7c2f235c21fe16b5ade9dbd23d19dbae`.

## Point 14 — SEO backend

Status: verified implementation; ready for coordinated server-first publication after the client point-14 gate is green.

Implemented:
- versioned `SeoEntry` / `SeoRevision` storage with history, optimistic concurrency, archive/unarchive, soft delete and revision restore;
- stable `PageId` support for database pages plus a constrained static-route registry; arbitrary, private, admin, alias and malformed targets are rejected;
- normalized duplicate/conflict checks across create, edit, unarchive and restore paths;
- canonical, social-image and URL validation, including media-reference lifecycle protection;
- public `/api/seo/snapshot` containing only current public/indexing inputs, published Page Builder targets, registry version and deterministic snapshot version;
- idempotent PostgreSQL upgrade/bootstrap SQL and PostgreSQL media-deactivation protection;
- production `ClientApp:BaseUrl` and `Api:PublicBaseUrl` fallbacks for the current Render origins while deployment environment variables remain authoritative.

Verification evidence:
- Server CI run 75 completed successfully with PostgreSQL 17;
- solution Release build completed with 0 errors;
- catalog model/security checks passed;
- controller + PostgreSQL integration suite: 699 total, 699 succeeded, 0 failed, 0 skipped;
- PostgreSQL integration exercised SEO SQL/locking/media-reference behavior rather than relying only on SQLite/InMemory substitutes.

Known pre-existing warnings remain outside point 14, including NU1903 advisories for `Microsoft.OpenApi` 2.0.0 and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 plus existing nullable/analyzer warnings. They did not fail the point-14 gate.

Publication to `main` is source-control publication, not proof of Render deployment. The final publication SHA is recorded by the server-first fast-forward after both server and client point-14 gates are green.

## Point 15 — roles and permissions

### 15.1 — inventory and persistence foundation

Status: COMPLETE on `work/roles-permissions-15-1`; intentionally not merged to `main` until the later point-15 enforcement stages are ready.

Implemented:
- centralized granular permission catalog with `read`, `create`, `update`, `archive`, `delete`, `restore` and `manage` operations where the resource supports them;
- permission coverage for users, roles, role-permission assignment, user-role assignment, Page Builder, page blocks/templates/presets, talent pages/trees, navigation, banners, footer, SEO, localization, database browsing, media, game-data resources, carts, products, reviews and promo codes;
- protected system-role catalog with the existing `Admin` role treated as the bootstrap administrator role;
- explicit `AdminEndpointRegistry` covering the existing legacy `Authorize(Roles = "Admin")` controller actions, Admin-only routes outside `/Admin`, mixed-access actions with an Admin override, and currently public Page Builder helper endpoints that require later hardening;
- reflection coverage that fails when an existing legacy Admin-authorized controller action is missing from the registry or references an unknown permission;
- role security persistence model for role profiles, role-permission grants and versioned security revisions;
- idempotent PostgreSQL `roles-permissions` upgrade that preserves existing Identity roles and memberships, protects referenced roles through FK constraints, and can be executed repeatedly;
- API startup wiring for the embedded roles/permissions database upgrade resource;
- Admin bootstrap grants for the complete known permission catalog without replacing existing Identity user-role membership.

Inventory findings intentionally deferred to the enforcement stage:
- `/api/presets` create/update/delete mutations are Admin-only but currently lack antiforgery validation;
- `/api/talents/{key}` save is Admin-only but currently lacks antiforgery validation;
- legacy `/Products/DeleteProduct` remains a destructive GET and must be removed or converted;
- Page Builder block preview helpers are currently public and are explicitly inventoried for permission hardening;
- product details and review deletion contain mixed normal-user/Admin behavior and therefore require permission-aware override logic instead of a blanket route lock.

Verification evidence:
- Server CI run 85 completed successfully against PostgreSQL 17;
- solution Release build: 0 errors;
- catalog model checks: passed;
- full controller + PostgreSQL integration suite: 709 total, 709 succeeded, 0 failed, 0 skipped;
- the PostgreSQL access-control integration executes the upgrade twice, verifies existing Admin membership survives, verifies the protected role profile, verifies indexes, and confirms referenced roles cannot be silently deleted;
- the first reflection run found ten missing legacy Admin GET actions; the registry was corrected rather than weakening the reflection guard, and the final full suite passed.

Scope boundary for 15.1:
- role CRUD API, permission mutation API and user-role assignment API are not implemented yet;
- runtime permission authorization/policies are not enforced yet; the application still uses the existing Admin-role protection until the enforcement stage replaces it safely;
- last-active-admin concurrency protection, self-escalation protection, live permission revocation, security-stamp/session refresh, full security audit logging and restore security checks are later point-15 stages;
- the admin Client UI and effective-permission route/menu/action gating are later point-15 stages.

15.1 completion means the inventory, permission vocabulary, persistence schema, bootstrap compatibility and automated coverage gate are finished. It does not mean point 15 as a whole is complete.

### 15.2 — role and user management backend

Status: COMPLETE on `work/roles-permissions-15-2`; intentionally unmerged while point 15 remains in staged security implementation.

Implemented:
- Admin-only, antiforgery-protected `/Admin/api/access-control` management API for role list/detail/create/update/delete, permission replacement, history/restore, user search, role membership assignment/revocation and durable audit browsing;
- role-name validation with trimming, control-character rejection and case-insensitive uniqueness through Identity `NormalizedName`;
- server-side validation that refuses unknown permission IDs instead of storing arbitrary strings;
- custom-role enable/disable state with disabled-role assignment blocked;
- optimistic role `Version` checks with stale writes rejected as conflicts;
- protected `Admin` invariants: reserved/protected name, cannot disable, cannot delete and cannot replace the full effective permission set with a reduced set;
- deletion of roles that still have users is blocked; there is no implicit user reassignment;
- versioned role snapshots and invariant-checked revision restore;
- durable `AccessControlAuditEntries` capturing actor, target role/user, action, UTC time and old/new state for role and membership changes;
- user-role assignment/revocation against the existing `AspNetUserRoles` table, not a parallel membership system;
- security-stamp rotation on membership assignment/revocation so later session enforcement can invalidate stale authorization state;
- self-demotion protection for administrators;
- last-active-administrator protection serialized through a PostgreSQL transaction advisory lock, so concurrent administrator revocations cannot both pass a stale count check;
- provider-neutral active-admin evaluation for unit tests plus a real PostgreSQL 17 concurrency race test using two parallel service instances;
- idempotent PostgreSQL upgrade extended with durable audit storage and indexes;
- access-control management actions added to the endpoint/permission registry, keeping the later enforcement stage mechanically traceable.

Verification evidence:
- Server CI run 94 completed successfully against PostgreSQL 17 before final documentation/analyzer-only cleanup;
- Release solution build: 0 errors;
- catalog model/security checks: passed;
- full suite at the 15.2 functional gate: 721 total, 721 succeeded, 0 failed, 0 skipped;
- PostgreSQL migration test executes the access-control upgrade twice, preserves existing Admin membership, verifies system profile/FK/index/audit-table behavior and keeps role deletion restricted while referenced;
- PostgreSQL concurrent-revocation test starts with two active Admin memberships, issues two concurrent revokes through separate service instances, proves exactly one succeeds/one is rejected, and verifies exactly one Admin membership remains;
- controller metadata tests verify the management controller retains the current `Admin` authorization boundary plus automatic antiforgery protection and that every new action has a known registry permission mapping.

Scope boundary for 15.2:
- the management API still uses the legacy `Authorize(Roles = "Admin")` boundary; granular permission policies/handlers are intentionally 15.3;
- broader admin endpoints are not yet permission-enforced; the security debt inventoried in 15.1 still belongs to 15.3;
- security-stamp rotation is implemented on membership mutation, but proving active-session revocation through the real HTTP authentication pipeline belongs to 15.4;
- user disable/delete/lockout administration is not introduced here, so last-admin protection in 15.2 applies to the role-assignment mutation path implemented in this stage;
- the Roles/Users admin Client UI is 15.5.

15.2 completion means the backend management and transactional safety layer is in place. It does not mean point 15 as a whole is complete.

### 15.3 — granular runtime permission enforcement

Status: COMPLETE on `work/roles-permissions-15-3`; intentionally unmerged while point 15 remains staged.

Implemented:
- centralized `IAdminPermissionEvaluator`, `AdminPermissionRequirement`, authorization handler and `AdminPermissionEnforcementMiddleware`;
- registry-driven authorization after authentication and before ASP.NET authorization, covering registered admin routes regardless of `/Admin` prefix;
- current database role membership/grant evaluation for every permission decision, excluding disabled roles and allowing a resource `manage` grant to satisfy its supported narrower operations;
- request-local compatibility elevation after a granular permission succeeds so legacy `Authorize(Roles = "Admin")` attributes remain a secondary boundary without persisting an Admin role into the Identity cookie;
- body-aware lifecycle authorization selecting `archive`, `delete` or `restore` permission and rewinding the request body before MVC binding;
- Page Builder `/api/blocks/render` and `/api/blocks/render-layout` preview helpers are no longer anonymous and require `page_blocks.read` through the central registry;
- preset mutation antiforgery hardening through `AutoValidateAntiforgeryToken`;
- talent-tree mutation antiforgery hardening through `AutoValidateAntiforgeryToken`;
- legacy product deletion GET is now non-destructive and returns HTTP 405; the DELETE mutation remains the supported antiforgery-protected path;
- product hidden-state visibility override now requires `products.read` rather than `User.IsInRole("Admin")`;
- review moderation override now requires `product_reviews.delete` rather than `User.IsInRole("Admin")`;
- mixed-access public/owner actions remain mixed instead of being incorrectly blanket-locked by the admin middleware;
- exhaustive regression coverage iterates every non-mixed `AdminEndpointRegistry` row and proves that controller/action/HTTP-method resolution reaches a granular permission decision, including overloaded product actions and lifecycle endpoints.

Verification evidence:
- Server CI run 98 completed successfully against PostgreSQL 17;
- Release solution build: 0 errors;
- catalog model/security checks: passed;
- full controller + PostgreSQL integration suite at the final 15.3 enforcement gate: 728 total, 728 succeeded, 0 failed, 0 skipped;
- explicit tests cover anonymous 401, missing-permission 403, successful custom-role permission authorization, lifecycle permission resolution/body rewind, mixed-access bypass, permission-aware product/review overrides, antiforgery metadata and non-destructive legacy GET behavior;
- draft PR #7 is mergeable/clean against current `main`; the branch is ahead of `main` and not behind it.

Scope boundary for 15.3:
- security-stamp rotation already occurs on membership mutations, but a real authenticated HTTP session must still prove that changed/revoked rights stop authorizing as expected; that is 15.4;
- anonymous/user/read-only/editor/admin end-to-end HTTP authorization matrices are 15.4, not claimed by the middleware unit/integration gate here;
- effective-permission client state, Roles/Users screens, route/menu/action hiding and responsive UI are 15.5;
- point 15 is not merged/published to `main` yet.

15.3 completion means the registered server admin surface is now granularly permission-enforced and the known endpoint-hardening debt from 15.1/15.2 is closed. It does not mean point 15 as a whole is complete.
