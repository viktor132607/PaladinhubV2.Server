# Languages and translations (point 11)

**Admin → Languages & translations** (`/Admin/Translations`) manages languages and their text dictionaries. English and Bulgarian are seeded once; additional language codes such as `de` and `pt-BR` can be added. Codes are immutable and reserved after deletion, so restore recovers the same identity. Names and translations remain editable.

Add a text key/value, edit its value, or remove it, then **Save language & translations**. Changes are saved atomically with a version check. Keys are searchable and paginated in groups of 50 without losing edits on other pages. The editor suggests common UI keys and current navigation IDs/names. To rename a key, add the replacement key and remove the old key in the same save.

History stores full dictionaries, names, status, actor and timestamp for every change. Languages support archive, unarchive, recoverable deletion and restoring an earlier revision. English cannot be archived/deleted because it is the fallback. Removing an individual translation falls back to the English dictionary, then to the source text; it does not remove site content. All admin mutations require Admin + CSRF, share the advisory transaction lock and reject stale versions.

## Live integration

The navbar language selector works on desktop/mobile and persists the selected code locally. Only active languages are offered. Unknown, archived or deleted selections fall back to English on reload/resource refresh. `html.lang` follows the language actually returned by the server. API failure leaves readable source text. Admin saves refresh the current tab's language resources.

Managed navigation uses `navigation.<id>` when present, then the original link name as a key. Built-in fallback navigation, account menu labels, cart label and footer text use their original English text as keys. This changes displayed labels without changing links or authentication behavior.

Page Builder content translates the visible text fields `text`, `title`, `label`, `caption`, `alt`, `alternative`, `description` and `name`, including nested blocks, while preserving stored source JSON, IDs, URLs, icons, CSS, ranks and talent prerequisites. For these fields, use the exact original text as the key. This covers content rendered by `DynamicPageContent`; hardcoded legacy guide prose and remaining standalone page labels are not automatically translated. Other components can opt in through `useLocalization().t(key, fallback)`.

## Storage and verification

`localization-upgrade.sql` is embedded in API startup behind the existing `APPLY_MIGRATIONS_ON_STARTUP` setting. It creates languages/revisions and seeds EN/BG plus baseline history idempotently without overwriting edits. History foreign keys restrict physical deletion. No production database was modified locally.

Verified: client production build and 17 unit tests; server/API build and catalog checks (input validation, protected fallback, versioning, FK, Admin/CSRF); a real controller read test with an EF in-memory store verifies language selection and English fallback; PGlite executes the SQL repeatedly, checks preservation and uniqueness/FK constraints; mocked-browser language/key CRUD, history restore, public navbar/footer updates, persisted selection and widths 428/926/1440. Browser emulation does not certify physical Safari behavior.
