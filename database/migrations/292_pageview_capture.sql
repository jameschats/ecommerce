-- =====================================================================
-- 292_pageview_capture.sql  —  Native traffic analytics: capture page-view
-- context (path, referrer/source, device, country) on CustomerEvents.
-- Enables owned GA-style traffic reports without a third-party tracker.
-- Idempotent. Country comes from Cloudflare's CF-IPCountry header (free); finer
-- geo (city/state) would need a GeoIP dataset (MaxMind) — deferred.
-- =====================================================================

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='CustomerEvents' AND COLUMN_NAME='Path');
SET @s := IF(@c=0, 'ALTER TABLE `CustomerEvents` ADD COLUMN `Path` VARCHAR(300) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='CustomerEvents' AND COLUMN_NAME='Referrer');
SET @s := IF(@c=0, 'ALTER TABLE `CustomerEvents` ADD COLUMN `Referrer` VARCHAR(200) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='CustomerEvents' AND COLUMN_NAME='Device');
SET @s := IF(@c=0, 'ALTER TABLE `CustomerEvents` ADD COLUMN `Device` VARCHAR(20) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='CustomerEvents' AND COLUMN_NAME='Country');
SET @s := IF(@c=0, 'ALTER TABLE `CustomerEvents` ADD COLUMN `Country` VARCHAR(2) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- Region/city from Cloudflare's "Add visitor location headers" managed transform (CF-Region / CF-IPCity),
-- if enabled on the zone. Null when unavailable (finer geo then falls back to country only).
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='CustomerEvents' AND COLUMN_NAME='Region');
SET @s := IF(@c=0, 'ALTER TABLE `CustomerEvents` ADD COLUMN `Region` VARCHAR(80) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='CustomerEvents' AND COLUMN_NAME='City');
SET @s := IF(@c=0, 'ALTER TABLE `CustomerEvents` ADD COLUMN `City` VARCHAR(80) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '292_pageview_capture.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '292_pageview_capture.sql');
