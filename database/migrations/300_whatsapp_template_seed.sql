-- =====================================================================
-- 300_whatsapp_template_seed.sql
--
-- Migration 265 added NotificationTemplates.ExternalTemplateId but never
-- seeded any WhatsApp rows — so there was nothing for the new admin
-- "Notifications" screen to show, and no row for NotificationRouter's
-- OrderStatusUpdate/OrderShipped chains (both already list WhatsApp
-- first) to ever find. Seeds a WhatsApp row for each on tenant 1 and
-- backfills every other tenant, same pattern migration 252 used for the
-- Email/SMS backfill. Seeded IsActive=0 and ExternalTemplateId=NULL —
-- WhatsAppNotificationChannel safely no-ops without a template id, but
-- inactive keeps it out of NotificationHistory until an admin turns it
-- on after Meta approves a real template and the id is filled in here.
-- Idempotent: UNIQUE (TenantId, Code, Channel) + INSERT IGNORE / NOT EXISTS.
-- =====================================================================

INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `ExternalTemplateId`, `IsActive`) VALUES
(1, 'OrderStatusUpdate', 'WhatsApp', NULL,
 'Hi {{CustomerName}}, your {{StoreName}} order {{OrderNumber}} is now {{Status}}.', NULL, 0),

(1, 'OrderShipped', 'WhatsApp', NULL,
 'Your {{StoreName}} order {{OrderNumber}} shipped via {{Courier}}. Track: {{TrackingNumber}}', NULL, 0);

INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `ExternalTemplateId`, `IsActive`)
SELECT t.`TenantId`, s.`Code`, s.`Channel`, s.`Subject`, s.`Body`, s.`ExternalTemplateId`, s.`IsActive`
  FROM `Tenants` t
  CROSS JOIN `NotificationTemplates` s
 WHERE s.`TenantId` = 1
   AND s.`Channel` = 'WhatsApp'
   AND t.`TenantId` <> 1
   AND NOT EXISTS (
        SELECT 1 FROM `NotificationTemplates` x
         WHERE x.`TenantId` = t.`TenantId` AND x.`Code` = s.`Code` AND x.`Channel` = s.`Channel`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '300_whatsapp_template_seed.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '300_whatsapp_template_seed.sql');
