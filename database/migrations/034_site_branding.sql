-- ---------------------------------------------------------------------------
-- 034_site_branding.sql — browser tab title and favicon, editable from admin
--
-- Both were hardcoded: the <title> in index.html and favicon.ico in the build output.
-- Changing either meant a code edit and a deploy, which is the wrong shape for something
-- a shop owner will want to adjust while settling on a brand.
--
-- Empty values mean "use the built-in defaults", so this migration changes nothing until
-- an admin fills it in.
-- ---------------------------------------------------------------------------

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT * FROM (
  SELECT 'Site.BrowserTitle' AS k, '' AS v, NOW() AS c UNION ALL
  SELECT 'Site.FaviconUrl',        '',      NOW()
) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k
);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '034_site_branding.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '034_site_branding.sql');
