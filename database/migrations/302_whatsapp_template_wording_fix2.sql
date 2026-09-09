-- =====================================================================
-- 302_whatsapp_template_wording_fix2.sql
--
-- Migration 301's revised wording still got rejected by Meta: "Variables
-- can't be at the start or end of the template" — both bodies ended
-- with their last {{token}} as the final (or effectively final, save a
-- lone period) content. Appends a short closing sentence after the last
-- variable in each so there's real static text on both sides, matching
-- the wording actually resubmitted for Meta review. Token order/names
-- unchanged, so NotificationRouter's positional-param derivation is
-- unaffected.
-- =====================================================================

UPDATE `NotificationTemplates`
   SET `Body` = 'Hello! This is an update from {{StoreName}} regarding your order {{OrderNumber}}. The current status of your order is: {{Status}}. Thank you for shopping with us!'
 WHERE `Channel` = 'WhatsApp' AND `Code` = 'OrderStatusUpdate';

UPDATE `NotificationTemplates`
   SET `Body` = 'Good news from {{StoreName}}! Your order {{OrderNumber}} has been shipped via {{Courier}}. You can track it using the following number: {{TrackingNumber}}. Thank you for shopping with us!'
 WHERE `Channel` = 'WhatsApp' AND `Code` = 'OrderShipped';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '302_whatsapp_template_wording_fix2.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '302_whatsapp_template_wording_fix2.sql');
