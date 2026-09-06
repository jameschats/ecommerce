-- ---------------------------------------------------------------------------
-- 059_social_links.sql — admin-configurable footer social links
--
-- The footer's Facebook/Instagram/X/LinkedIn icons were hardcoded with href="#"
-- placeholders. Each now has a URL plus its own show/hide flag, so a shop with no
-- real profile yet can keep an icon hidden instead of publishing a dead link.
-- Enabled defaults to false: the icons disappear until admin fills in a real URL
-- and switches them on, which is strictly better than the dead "#" links they
-- replace. Settings is an EAV table, so this is just seeding new keys.
-- ---------------------------------------------------------------------------

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT k, v, NOW() FROM (
  SELECT 'Social.FacebookUrl' AS k, '' AS v
  UNION ALL SELECT 'Social.FacebookEnabled', 'false'
  UNION ALL SELECT 'Social.InstagramUrl', ''
  UNION ALL SELECT 'Social.InstagramEnabled', 'false'
  UNION ALL SELECT 'Social.XUrl', ''
  UNION ALL SELECT 'Social.XEnabled', 'false'
  UNION ALL SELECT 'Social.LinkedinUrl', ''
  UNION ALL SELECT 'Social.LinkedinEnabled', 'false'
) AS seed
WHERE NOT EXISTS (SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '059_social_links.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '059_social_links.sql');
