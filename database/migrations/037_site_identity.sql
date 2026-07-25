-- ---------------------------------------------------------------------------
-- 037_site_identity.sql — storefront site name and header logo, editable from admin
--
-- The name in the header and footer was hardcoded as "CalendarShop" in app.html, so
-- rebranding meant a code edit and a deploy. Same story as 034 did for the browser tab;
-- these two keys join that family and are served by the same /api/site/branding endpoint.
--
-- Empty values mean "use the built-in default" — an unconfigured shop renders exactly
-- the two-tone CalendarShop wordmark it did before, with no logo.
-- ---------------------------------------------------------------------------

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT * FROM (
  SELECT 'Site.Name' AS k, '' AS v, NOW() AS c UNION ALL
  SELECT 'Site.LogoUrl',    '',      NOW()
) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k
);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '037_site_identity.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '037_site_identity.sql');
