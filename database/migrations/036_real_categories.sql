-- ---------------------------------------------------------------------------
-- 036_real_categories.sql — the actual product categories
--
-- Replaces the demo categories (Wall / Desk / Tent / Pocket / Magnet / Mouse Pad) with the
-- real ones supplied by the business.
--
-- The old categories are DEACTIVATED, not deleted. Deleting them would break the foreign
-- key from any product still pointing at one, and other tables may reference categories
-- too. Deactivating hides them from the storefront and every admin dropdown, is reversible,
-- and lets the rows be removed later once the real catalogue import has replaced the demo
-- products entirely.
-- ---------------------------------------------------------------------------

-- 1) The real categories -------------------------------------------------------------
INSERT INTO `Categories` (`TenantId`, `ParentCategoryId`, `Name`, `Slug`, `DisplayOrder`, `IsActive`, `CreatedAt`)
SELECT * FROM (
  SELECT 1 AS t, NULL AS p, 'Calendar Mount - 10" x 15" Varnish'       AS n, 'calendar-mount-10x15-varnish'      AS s, 10 AS d, 1 AS a, NOW() AS c UNION ALL
  SELECT 1, NULL, 'Calendar Mount - 10" x 15" Plain Art',                    'calendar-mount-10x15-plain-art',         20, 1, NOW() UNION ALL
  SELECT 1, NULL, 'Calendar Mount - 10" x 15" Lamination Art',               'calendar-mount-10x15-lamination-art',    30, 1, NOW() UNION ALL
  SELECT 1, NULL, 'Calendar Mount - 10" x 15" Fancy Cut',                    'calendar-mount-10x15-fancy-cut',         40, 1, NOW() UNION ALL
  SELECT 1, NULL, 'Panchangam',                                              'panchangam',                             50, 1, NOW() UNION ALL
  SELECT 1, NULL, 'Cake - size 4',                                           'cake-size-4',                            60, 1, NOW() UNION ALL
  SELECT 1, NULL, 'Cake - size 5',                                           'cake-size-5',                            70, 1, NOW() UNION ALL
  SELECT 1, NULL, 'Cake - size 6',                                           'cake-size-6',                            80, 1, NOW() UNION ALL
  SELECT 1, NULL, 'Cake - size 7',                                           'cake-size-7',                            90, 1, NOW()
) AS seed
WHERE NOT EXISTS (SELECT 1 FROM `Categories` c WHERE c.`Slug` = seed.s);

-- 2) Move any surviving products off the demo categories ------------------------------
--    Products must always point at a live category, or they vanish from the price list,
--    which groups by category.
SET @fallback := (SELECT `CategoryId` FROM `Categories` WHERE `Slug` = 'calendar-mount-10x15-varnish' LIMIT 1);

UPDATE `Products`
   SET `CategoryId` = @fallback
 WHERE `CategoryId` IN (
   SELECT * FROM (
     SELECT `CategoryId` FROM `Categories`
      WHERE `Slug` IN ('wall-calendars','desk-calendars','tent-calendars',
                       'pocket-calendars','magnet-calendars','mouse-pad-calendars')
   ) AS old_ids
 );

-- 3) Retire the demo categories --------------------------------------------------------
UPDATE `Categories`
   SET `IsActive` = 0, `UpdatedAt` = NOW()
 WHERE `Slug` IN ('wall-calendars','desk-calendars','tent-calendars',
                  'pocket-calendars','magnet-calendars','mouse-pad-calendars');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '036_real_categories.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '036_real_categories.sql');
