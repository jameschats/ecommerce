-- =====================================================================
-- 018_theme_defaults.sql  —  CalendarShop brand colors (theme data)
-- Runtime theme data (also editable in Admin → Theme). Indigo primary.
-- =====================================================================

UPDATE `ThemeSettings` ts
JOIN `Themes` t ON t.`ThemeId` = ts.`ThemeId` AND t.`TenantId` = 1 AND t.`IsActive` = 1
SET ts.`SettingValue` = '#4f46e5', ts.`UpdatedAt` = CURRENT_TIMESTAMP
WHERE ts.`SettingKey` = 'PrimaryColor';

UPDATE `ThemeSettings` ts
JOIN `Themes` t ON t.`ThemeId` = ts.`ThemeId` AND t.`TenantId` = 1 AND t.`IsActive` = 1
SET ts.`SettingValue` = '#0f172a', ts.`UpdatedAt` = CURRENT_TIMESTAMP
WHERE ts.`SettingKey` = 'SecondaryColor';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '018_theme_defaults.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '018_theme_defaults.sql');
