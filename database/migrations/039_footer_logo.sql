-- ---------------------------------------------------------------------------
-- 039_footer_logo.sql — a second logo for the footer
--
-- The header sits on white and the footer on slate-900. One image cannot suit both:
-- a logo with a white plate looks like a white box on the dark footer, and once the
-- background is removed, dark artwork disappears there instead.
--
-- So the footer gets its own image. Empty means "reuse the header logo", which is
-- exactly what every existing shop already does — this migration changes nothing
-- until someone uploads a light version.
-- ---------------------------------------------------------------------------

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT * FROM (SELECT 'Site.FooterLogoUrl' AS k, '' AS v, NOW() AS c) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k
);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '039_footer_logo.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '039_footer_logo.sql');
