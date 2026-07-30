-- =====================================================================
-- 043_banner_pages.sql  —  Page-scoped banners
-- HomeBanners previously powered only the home carousel. Adds a `Page` column so the
-- same table can hold banners for Order Now, Finished Calendar and About Us too, each
-- managed as its own tab in admin. Existing rows default to 'home' — no data loss.
-- Forward-only; idempotent.
-- =====================================================================

SET @col_exists := (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'HomeBanners' AND COLUMN_NAME = 'Page'
);
SET @sql := IF(@col_exists = 0,
    'ALTER TABLE `HomeBanners` ADD COLUMN `Page` VARCHAR(30) NOT NULL DEFAULT ''home'' AFTER `TenantId`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @idx_exists := (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'HomeBanners' AND INDEX_NAME = 'ix_homebanners_page'
);
SET @sql := IF(@idx_exists = 0,
    'ALTER TABLE `HomeBanners` ADD INDEX `ix_homebanners_page` (`TenantId`, `Page`, `IsActive`, `DisplayOrder`)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '043_banner_pages.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '043_banner_pages.sql');
