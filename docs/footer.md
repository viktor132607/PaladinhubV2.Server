# Managed footer and contacts

`/Admin/Footer` manages sections and text, link, email, phone, social and copyright entries. Entries have a real parent-section FK. Move entries by changing their section; use sort order for ordering. Archive hides a section and its children. Delete is recoverable, and deleting a section with non-deleted children is refused. Restore validates the destination section and current name conflicts. History contains actor/time and full snapshots; all mutations require Admin/CSRF, version matching and the shared advisory transaction lock.

Public `/api/footer` returns only visible sections and children. An intentional empty response displays no entries; a failed request displays the preexisting footer. Copyright supports `{year}`. Public text uses stable `footer.<id>.text` translation keys, shown in the editor. Contact URLs use typed email/phone fields; links accept internal or HTTP(S) addresses. Social icons are restricted to the existing Font Awesome set. No link changes are introduced by translations.

The startup-gated `footer-upgrade.sql` adds entries and revision tables, section/revision FKs and baseline copyright content. It is repeatable without overwriting edits. Source content from V1/V2 is preserved initially.

Verification: production client/server builds, 11 footer validation/controller/public-read tests, repeated PGlite SQL application with edit preservation and FK checks. Physical Safari and production migration are not claimed. Browser workflow evidence is recorded in the expansion tracker after validation.
