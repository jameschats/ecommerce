-- =====================================================================
-- 057_gallery_sections.sql  —  Two independent home-page galleries
-- Adds a Section column to GalleryImages so the admin can manage the "New designs" strip
-- (top of home, below the banner) and a second "featured" strip (below the price list)
-- as two separate photo sets, same idea as HomeBanners.Page. Existing rows default to
-- "new-designs" so today's gallery keeps showing exactly where it already does.
-- Forward-only; idempotent.
-- =====================================================================

SET @col_exists := (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GalleryImages' AND COLUMN_NAME = 'Section'
);
SET @sql := IF(@col_exists = 0,
    'ALTER TABLE `GalleryImages` ADD COLUMN `Section` VARCHAR(30) NOT NULL DEFAULT ''new-designs'' AFTER `TenantId`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @idx_exists := (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GalleryImages' AND INDEX_NAME = 'ix_galleryimages_section'
);
SET @sql := IF(@idx_exists = 0,
    'ALTER TABLE `GalleryImages` ADD INDEX `ix_galleryimages_section` (`TenantId`, `Section`, `IsActive`, `DisplayOrder`)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '057_gallery_sections.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '057_gallery_sections.sql');
