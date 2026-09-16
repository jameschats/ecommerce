-- =====================================================================
-- 306_whatsapp_template_seed_2.sql
--
-- The other two order-lifecycle codes NotificationRouter's ChannelChains
-- already lists WhatsApp first for (OrderConfirmation, OrderCancelled)
-- had no WhatsApp NotificationTemplate row at all — so, exactly like
-- OrderStatusUpdate/OrderShipped before migration 300, WhatsApp silently
-- falls through to Email every time for these two, template-not-found.
-- Seeds a WhatsApp row for each on tenant 1 and backfills every other
-- tenant, same pattern as 300.
--
-- Wording is written the first time already applying the two lessons the
-- 301/302 rejection-and-fix rounds learned the hard way (see
-- messaging-providers-state memory): (1) keep the variable-to-text ratio
-- low — Meta rejects a body with too many variables for its length; (2)
-- never open or close on a variable — real static text must sit before
-- the first {{n}} and after the last one, a trailing period doesn't
-- count. Not yet submitted to Gupshup/Meta — seeded IsActive=0,
-- ExternalTemplateId=NULL, same inert-until-approved convention as
-- migration 300. If Meta still rejects this draft, fix wording via a new
-- higher-numbered migration exactly like 301/302 did, never edit this one.
-- Idempotent: UNIQUE (TenantId, Code, Channel) + INSERT IGNORE / NOT EXISTS.
-- =====================================================================

INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `ExternalTemplateId`, `IsActive`) VALUES
(1, 'OrderConfirmation', 'WhatsApp', NULL,
 'Thank you for your order from {{StoreName}}! Your order {{OrderNumber}} for {{OrderTotal}} has been confirmed and is being processed. Thank you for shopping with us!', NULL, 0),

(1, 'OrderCancelled', 'WhatsApp', NULL,
 'We''re sorry to let you know that your order {{OrderNumber}} from {{StoreName}} has been cancelled. If a payment was made, your refund is being processed. Thank you for shopping with us!', NULL, 0);

INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `ExternalTemplateId`, `IsActive`)
SELECT t.`TenantId`, s.`Code`, s.`Channel`, s.`Subject`, s.`Body`, s.`ExternalTemplateId`, s.`IsActive`
  FROM `Tenants` t
  CROSS JOIN `NotificationTemplates` s
 WHERE s.`TenantId` = 1
   AND s.`Channel` = 'WhatsApp'
   AND s.`Code` IN ('OrderConfirmation', 'OrderCancelled')
   AND t.`TenantId` <> 1
   AND NOT EXISTS (
        SELECT 1 FROM `NotificationTemplates` x
         WHERE x.`TenantId` = t.`TenantId` AND x.`Code` = s.`Code` AND x.`Channel` = s.`Channel`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '306_whatsapp_template_seed_2.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '306_whatsapp_template_seed_2.sql');
