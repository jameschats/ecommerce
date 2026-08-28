-- =====================================================================
-- 282_product_recommendation_controls.sql  —  AI Commerce C4 merchant controls.
-- Per-product recommendation overrides: exclude a product from all recommendation
-- placements (e.g. discontinued stock), or pin it to always appear. Idempotent.
-- =====================================================================

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'ExcludeFromRecommendations');
SET @s := IF(@c = 0, 'ALTER TABLE `Products` ADD COLUMN `ExcludeFromRecommendations` TINYINT(1) NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'PinnedInRecommendations');
SET @s := IF(@c = 0, 'ALTER TABLE `Products` ADD COLUMN `PinnedInRecommendations` TINYINT(1) NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '282_product_recommendation_controls.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '282_product_recommendation_controls.sql');
