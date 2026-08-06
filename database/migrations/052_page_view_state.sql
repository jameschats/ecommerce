-- =====================================================================
-- 052_page_view_state.sql  —  State/province on PageViews
-- GeoLite2-City already returns a subdivision (state/province, e.g. "Tamil Nadu") alongside
-- country/city — this column just captures it so traffic can be reported by state, not only
-- by city. Forward-only; idempotent.
-- =====================================================================

SET @col_exists := (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PageViews' AND COLUMN_NAME = 'State'
);
SET @sql := IF(@col_exists = 0,
    'ALTER TABLE `PageViews` ADD COLUMN `State` VARCHAR(100) NULL AFTER `Country`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '052_page_view_state.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '052_page_view_state.sql');
