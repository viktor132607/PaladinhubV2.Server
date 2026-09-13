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
