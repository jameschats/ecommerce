-- ---------------------------------------------------------------------------
-- 048_abandoned_estimates.sql — make abandonment observable
--
-- Cart.Status has documented 'Abandoned' since 005 and nothing has ever written it,
-- because the live flow is quick-order: quantities live in the browser's localStorage
-- and the Cart table is never touched. So there was nothing to report on.
--
-- Rather than a second basket table, the estimate is now mirrored into Cart for
-- signed-in shoppers, which revives the table that was built for exactly this.
--
-- Only signed-in shoppers are tracked, and that costs nothing: an abandoned basket is
-- only actionable if you can email the person, and an anonymous visitor has left no
-- address to email. AbandonedRemindedAt records that we have already chased them, so a
-- reminder is not sent twice for one basket.
-- ---------------------------------------------------------------------------

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cart' AND COLUMN_NAME = 'AbandonedRemindedAt');
SET @sql := IF(@c = 0,
  'ALTER TABLE `Cart` ADD COLUMN `AbandonedRemindedAt` DATETIME NULL AFTER `Status`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Finding stale baskets is the whole query this feature runs.
SET @i := (SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cart' AND INDEX_NAME = 'ix_cart_status_updated');
SET @sql := IF(@i = 0,
  'CREATE INDEX `ix_cart_status_updated` ON `Cart` (`Status`, `UpdatedAt`)',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '048_abandoned_estimates.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '048_abandoned_estimates.sql');
