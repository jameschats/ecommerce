-- ---------------------------------------------------------------------------
-- 041_finished_calendar.sql — the Finished Calendar category and its own page
--
-- Finished Calendar is sold from a page of its own, so it must not appear in the
-- main price list on the home and Order Now screens.
--
-- The exclusion is a column rather than a hardcoded slug in the service. A name
-- baked into C# breaks silently the day someone renames the category in admin,
-- and gives no way to do the same for a second range later.
--
-- Default 1 so every existing category keeps showing exactly where it does today.
-- ---------------------------------------------------------------------------

-- 1) The flag -------------------------------------------------------------------------
--    Guarded: re-running must not fail on an already-added column.
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'Categories'
    AND COLUMN_NAME  = 'ShowInPriceList'
);

SET @sql := IF(@col_exists = 0,
  'ALTER TABLE `Categories` ADD COLUMN `ShowInPriceList` TINYINT(1) NOT NULL DEFAULT 1 AFTER `IsActive`',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 2) The category ---------------------------------------------------------------------
--    DisplayOrder 100 puts it after the nine categories seeded by 036.
INSERT INTO `Categories`
  (`TenantId`, `ParentCategoryId`, `Name`, `Slug`, `DisplayOrder`, `IsActive`, `ShowInPriceList`, `CreatedAt`)
SELECT * FROM (
  SELECT 1 AS t, NULL AS p, 'Finished Calendar' AS n, 'finished-calendar' AS s,
         100 AS d, 1 AS a, 0 AS spl, NOW() AS c
) AS seed
WHERE NOT EXISTS (SELECT 1 FROM `Categories` c WHERE c.`Slug` = seed.s);

-- Idempotent for the case where the category was created by hand in admin first.
UPDATE `Categories` SET `ShowInPriceList` = 0 WHERE `Slug` = 'finished-calendar';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '041_finished_calendar.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '041_finished_calendar.sql');
