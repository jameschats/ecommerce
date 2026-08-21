-- =====================================================================
-- 263_notification_router.sql — v4 Phase 1, Track A: Notification Channel
-- Router. NotificationHistory gains AttemptGroupId/AttemptNumber so a
-- primary send and its fallback attempts (e.g. WhatsApp failed -> SMS
-- tried -> succeeded) can be correlated as one logical notification
-- instead of three unrelated rows. Purely additive; existing rows get
-- AttemptNumber=1, AttemptGroupId=NULL (they predate the router).
-- =====================================================================

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'NotificationHistory' AND COLUMN_NAME = 'AttemptGroupId');
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `NotificationHistory`
     ADD COLUMN `AttemptGroupId` CHAR(36) NULL AFTER `SentAt`,
     ADD COLUMN `AttemptNumber` INT NOT NULL DEFAULT 1 AFTER `AttemptGroupId`,
     ADD KEY `IX_NotificationHistory_AttemptGroupId` (`AttemptGroupId`)',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '263_notification_router.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '263_notification_router.sql');
