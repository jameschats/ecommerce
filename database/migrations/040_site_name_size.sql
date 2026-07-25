-- ---------------------------------------------------------------------------
-- 040_site_name_size.sql — font size of the storefront wordmark
--
-- The name in the header and footer was fixed at text-xl (1.25rem). How large it
-- wants to be depends on the name and on whether a logo sits beside it — a short
-- mark next to a 36px logo reads small at 20px, a long one crowds the row at 32px.
--
-- Stored as the rem value itself so the template needs no lookup table. Empty means
-- keep the built-in 1.25rem, so nothing moves until someone picks a size.
-- ---------------------------------------------------------------------------

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT * FROM (SELECT 'Site.NameSize' AS k, '' AS v, NOW() AS c) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k
);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '040_site_name_size.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '040_site_name_size.sql');
