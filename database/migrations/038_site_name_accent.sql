-- ---------------------------------------------------------------------------
-- 038_site_name_accent.sql — the highlighted tail of the storefront wordmark
--
-- The built-in wordmark was two-tone: "Calendar" plus "Shop" in the primary colour.
-- Migration 037 made the name configurable but could only render it in one colour,
-- because a single string carries no split point — so configuring a name silently
-- lost the accent.
--
-- This holds the highlighted part. It is concatenated onto Site.Name with no
-- separator, so "Daily" + "Calendar" renders DailyCalendar with the tail in the
-- primary colour. Empty means the whole name is one colour.
-- ---------------------------------------------------------------------------

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT * FROM (SELECT 'Site.NameAccent' AS k, '' AS v, NOW() AS c) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k
);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '038_site_name_accent.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '038_site_name_accent.sql');
