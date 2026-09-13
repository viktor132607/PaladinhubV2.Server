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
