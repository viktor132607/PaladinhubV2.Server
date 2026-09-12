CREATE TABLE IF NOT EXISTS "SiteLanguages" (
 "Id" uuid PRIMARY KEY, "Code" varchar(35) NOT NULL, "Name" varchar(100) NOT NULL,
 "ResourcesJson" text NOT NULL, "IsArchived" boolean NOT NULL DEFAULT false,
 "IsDeleted" boolean NOT NULL DEFAULT false, "Version" integer NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SiteLanguages_Code" ON "SiteLanguages"("Code");
CREATE TABLE IF NOT EXISTS "LanguageRevisions" (
 "Id" uuid PRIMARY KEY, "LanguageId" uuid NOT NULL REFERENCES "SiteLanguages"("Id") ON DELETE RESTRICT,
 "Version" integer NOT NULL, "Action" varchar(30) NOT NULL, "Actor" varchar(100) NOT NULL,
 "Snapshot" text NOT NULL, "CreatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_LanguageRevisions_LanguageId_Version" ON "LanguageRevisions"("LanguageId", "Version");
INSERT INTO "SiteLanguages" ("Id", "Code", "Name", "ResourcesJson") VALUES
('00000000-0000-0000-0000-000000001101','en','English',json_build_object()::text),
('00000000-0000-0000-0000-000000001102','bg','Български',json_build_object('Home', 'Начало', 'Holy Paladin', 'Свещен паладин', 'Protection Paladin', 'Паладин защитник', 'Retribution Paladin', 'Паладин възмездие', 'Overview', 'Преглед', 'Gear', 'Екипировка', 'Talents', 'Таланти', 'Consumables', 'Консумативи', 'Rotation', 'Ротация', 'Stats', 'Показатели', 'Discussion', 'Дискусии', 'Discussions', 'Дискусии', 'Privacy', 'Поверителност', 'Merchandise', 'Магазин', 'Login', 'Вход', 'Register', 'Регистрация', 'My Account', 'Моят профил', 'Settings', 'Настройки', 'Change Password', 'Промяна на парола', 'Logout', 'Изход', 'Logging out...', 'Излизане...', 'My Cart', 'Моята количка', 'Language', 'Език', 'Toggle navigation', 'Отвори навигацията', 'Made with 💛 for WoW Paladins', 'Създадено с 💛 за паладините в WoW')::text)
ON CONFLICT ("Code") DO NOTHING;
INSERT INTO "LanguageRevisions" ("Id", "LanguageId", "Version", "Action", "Actor", "Snapshot", "CreatedAtUtc")
SELECT gen_random_uuid(), l."Id", l."Version", 'imported', 'system', row_to_json(l)::text, NOW()
FROM "SiteLanguages" l WHERE NOT EXISTS (SELECT 1 FROM "LanguageRevisions" r WHERE r."LanguageId" = l."Id");
