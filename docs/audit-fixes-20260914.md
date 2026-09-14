# Audit corrections — 2026-09-14

- Lifecycle authorization rejects duplicate case-insensitive action keys and rewinds the body. Regression tests cover every multi-operation registry endpoint.
- Discussion moderation uses live discussion_posts.delete permission; ownership is retained. Stale Admin claims alone do not authorize deletion.
- Self-escalation checks execute inside the mutation transaction and shared advisory lock. The controller no longer performs a separate stale check.
- Deleted roles retain their identity, grants and revision history, remain disabled and cannot be assigned or edited until a pre-delete revision is restored. Existing memberships must be removed before deletion; restoration creates none. Names remain reserved. Roles hard-deleted by older releases cannot be recovered from missing revisions.
- Role snapshots remain compatible with the stored PascalCase contract.
- Managed image URL namespace is reserved: new SEO records must select tracked media instead. Legacy URL references block media deactivation through both the service and SQL trigger. Apply the embedded idempotent SEO upgrade on deployment.
- Public snapshots include product and discussion detail metadata, exclude physically deleted records, and hash complete output plus origins. Product detail aliases share a canonical path.
- Locked users lose effective administrative permissions on the next evaluation.
- Missing PostgreSQL configuration is now an explicit test skip; CI supplies PostgreSQL 17. Local unit/SQLite checks are not presented as PostgreSQL verification.
