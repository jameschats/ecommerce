-- ---------------------------------------------------------------------------
-- 054_order_delivered_template.sql — a real Delivered message, and tidying two dead rows
--
-- Dispatch and delivery each sent the customer TWO emails. AdminOrdersController called the
-- service (which notifies from a template) and then called OrderMailer (hardcoded HTML)
-- for the same event, seconds apart. Neither knew about the other — the cost of having two
-- email systems side by side.
--
-- The template path wins, because those messages are editable and can be switched off from
-- admin, and the hardcoded ones can be neither. But delivery had no template of its own: it
-- reused the generic "OrderStatusUpdate", which reads far worse than the purpose-written
-- mail it would replace. So it gets one.
--
-- Also removes `order.confirmation` and `password.reset`. Nothing reads them — the code uses
-- the PascalCase codes — so editing or disabling either silently did nothing, which is worse
-- than their not being there at all.
-- ---------------------------------------------------------------------------

INSERT INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `IsActive`, `CreatedAt`)
SELECT 1, 'OrderDelivered', 'Email',
       'Order {{OrderNumber}} delivered',
       CONCAT('<p>Hi {{CustomerName}},</p>',
              '<p>Your order <strong>{{OrderNumber}}</strong> has been delivered. ',
              'We hope everything arrived in good order.</p>',
              '<p>If anything is not right, reply to this email and we will put it straight.</p>',
              '<p>— {{StoreName}}</p>'),
       1, NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM `NotificationTemplates`
  WHERE `TenantId` = 1 AND `Code` = 'OrderDelivered' AND `Channel` = 'Email');

INSERT INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `IsActive`, `CreatedAt`)
SELECT 1, 'OrderDelivered', 'SMS', NULL,
       'Order {{OrderNumber}} delivered. Thank you for shopping with {{StoreName}}.',
       1, NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM `NotificationTemplates`
  WHERE `TenantId` = 1 AND `Code` = 'OrderDelivered' AND `Channel` = 'SMS');

-- Dead rows: nothing has ever read these codes.
DELETE FROM `NotificationTemplates`
 WHERE `TenantId` = 1 AND `Code` IN ('order.confirmation', 'password.reset');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '054_order_delivered_template.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '054_order_delivered_template.sql');
