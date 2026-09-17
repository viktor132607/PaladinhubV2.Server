# Full database backup and restore

Admin → Database backup (`/Admin/Backup`) downloads a compressed PostgreSQL custom-format `.dump` archive and restores an uploaded archive after typing `RESTORE`.

The export is an unfiltered `pg_dump`: every application schema and table, all rows (including users, password hashes, MFA data, roles, balances, orders, page history, media bytes and translations), sequences, relationships, indexes, views, functions, extensions and large objects. New tables are included automatically. Files stored outside PostgreSQL, environment secrets and cluster-wide PostgreSQL roles are not part of an application database dump. Keep downloads confidential.

Permissions: `database_backups.read` exports; `database_backups.restore` restores. The protected Admin system role receives both automatically; custom roles must receive explicit grants. Ordinary database browsing does not grant backup access. Restore uses the existing cookie authentication and CSRF protection.

Restore validates the archive header and table of contents and fully decompresses it before modifying PostgreSQL. It resets all non-system schemas, non-default extensions and large objects, then rebuilds from the archive in one `psql --single-transaction` transaction with `ON_ERROR_STOP`. Objects added after the snapshot are removed. Failure rolls back database changes. Ownership and ACLs are mapped to the configured application database account for portability; database contents and object definitions are restored. The account needs permission to drop/recreate all application objects and extensions. PostgreSQL system catalogs are not replaced.

The API streams downloads from temporary files and removes temporary files afterwards. Operations are serialized within the server process. Perform restores during a maintenance window without other application writers or multiple API instances. Refresh the site afterwards; restored accounts/permissions may require signing in again. The configured startup schema upgrades still apply on subsequent application restarts.

The runtime image installs PostgreSQL 18 `pg_dump`, `pg_restore` and `psql`. For local execution install those tools in PATH at a version at least as new as the server and compatible with the archive. The existing resolved database connection is used. No separate backup credentials are required. Large uploads also depend on upstream proxy request limits and timeouts.

CI runs a disposable PostgreSQL round-trip test using `BACKUP_TEST_CONNECTION`: cross-schema data, foreign keys, binary values, views, sequences and large objects; removal of post-snapshot objects; and rollback after a deliberately failing restore. It never connects to the production database.
