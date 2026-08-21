-- =====================================================================
-- 265_whatsapp_channel.sql — v4 Phase 1, Track C: WhatsApp BSP Connector
-- (Gupshup). Meta requires WhatsApp template messages to reference a
-- pre-approved template by the BSP's own template id, not by the
-- rendered text the way Email/SMS do — so NotificationTemplates gains
-- ExternalTemplateId, only meaningful for Channel='WhatsApp' rows.
-- Body still holds the human-readable approved wording with {{token}}
-- placeholders (same convention as every other channel); the router
-- derives Meta's positional {{1}},{{2}}... parameters from the order
-- those placeholders appear in Body, so no separate "parameter order"
-- column is needed.
-- =====================================================================

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'NotificationTemplates' AND COLUMN_NAME = 'ExternalTemplateId');
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `NotificationTemplates`
     ADD COLUMN `ExternalTemplateId` VARCHAR(150) NULL AFTER `Body`',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '265_whatsapp_channel.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '265_whatsapp_channel.sql');
