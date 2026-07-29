-- =====================================================================
-- 256_theme_settings_text.sql  —  Widen ThemeSettings.SettingValue for
-- structured (JSON) setting values.
--
-- Every ThemeSettings value so far has been a short scalar (a hex colour,
-- a font name, a keyword) that fits comfortably in VARCHAR(500). Named
-- colour schemes (T14) need to store a JSON array of several named
-- palettes under one setting key ("ColorSchemes"), which can exceed that.
-- The platform-wide `Settings` table already uses TEXT for the same
-- reason (011_platform.sql) — same fix here, same precedent.
-- =====================================================================

ALTER TABLE `ThemeSettings` MODIFY COLUMN `SettingValue` TEXT NULL;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '256_theme_settings_text.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '256_theme_settings_text.sql');
