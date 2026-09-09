-- =====================================================================
-- 301_whatsapp_template_wording.sql
--
-- Migration 300's seeded WhatsApp Body wording got rejected by Gupshup's
-- own client-side check when actually submitting to Meta for approval:
-- "too many variables for its length" (4 variables in a 46-character
-- OrderStatusUpdate body). Revises both bodies to the wording actually
-- submitted for Meta review — OrderStatusUpdate drops {{CustomerName}}
-- (the least essential of the four, and the cleanest way to fix the
-- ratio without cutting real information); OrderShipped keeps all four
-- tokens but with more surrounding static text. Token order still
-- matches the approved templates' positional {{1}},{{2}},... exactly,
-- so NotificationRouter's derive-order-from-Body-placeholders logic
-- keeps working with no code change.
-- =====================================================================

UPDATE `NotificationTemplates`
   SET `Body` = 'Hello! This is an update from {{StoreName}} regarding your order {{OrderNumber}}. The current status of your order is: {{Status}}.'
 WHERE `Channel` = 'WhatsApp' AND `Code` = 'OrderStatusUpdate';

UPDATE `NotificationTemplates`
   SET `Body` = 'Good news from {{StoreName}}! Your order {{OrderNumber}} has been shipped via {{Courier}}. You can track it using the following number: {{TrackingNumber}}'
 WHERE `Channel` = 'WhatsApp' AND `Code` = 'OrderShipped';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '301_whatsapp_template_wording.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '301_whatsapp_template_wording.sql');
