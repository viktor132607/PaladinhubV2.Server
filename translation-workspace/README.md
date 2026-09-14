# Point 16 — server source inventory

Run python scripts/inventory-server-translations.py from the repository root. The report records every C# source file and all lexically extracted string literals, including tests, SQL and protocol strings. Review-context means unclassified; it is not evidence of translated messages. Interpolated/raw strings require review of their complete C# expression before extracting user-facing templates. Do not translate routes, identifiers, SQL, user content or proper names.

Pending: classify source records individually, introduce stable public error/message keys with EN/BG catalog values, and preserve existing response compatibility. This checkpoint does not mark point 16 complete.

The complete report is versioned as `server-inventory.json.gz`; regeneration also writes readable `server-inventory.json`. No records are omitted by compression.
