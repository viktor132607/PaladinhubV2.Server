# Reusable blocks

In Page Builder → Create/Edit, expand **Reusable blocks**. Save one selected block or all current blocks as a named template. Search and select a template, then **Insert copy**. The copy is editable in the existing visual builder. Save the page normally. To revise a template, insert its content into a working page, edit it, select the relevant block(s) and choose **Replace template content**. Name and description can be saved independently.

Templates support archive/unarchive, recoverable deletion, full revision snapshots, actor/time history and optimistic version checks. Archived/deleted templates cannot be inserted or edited. A restore checks name conflicts and validates content again. All mutations require Admin and CSRF and use the shared database transaction lock. Revision FKs prevent physical deletion of history-bearing templates.

Reuse is by copy: template edits/deletion never alter inserted pages. Dynamic tree copies receive fresh tree/node IDs and remapped prerequisites. There are no live page references requiring a deletion restriction; saved copies remain usable independently.

`docs/templates-upgrade.sql` is embedded and applied with the existing `APPLY_MIGRATIONS_ON_STARTUP` gate. It creates two tables and a revision uniqueness index idempotently. No existing page content is migrated.

Validation: client production build and copy-isolation unit tests; server model/validation/auth/CSRF checks; PGlite execution of the upgrade twice with preserved records, FK and version uniqueness checks; browser create/insert/rename/archive/delete/restore flows with mocked API at 428, 926 and 1440 CSS pixels. Production database migration and physical iPhone Safari are not exercised locally.
