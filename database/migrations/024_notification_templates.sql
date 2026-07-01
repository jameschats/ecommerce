-- =====================================================================
-- 024_notification_templates.sql  —  Stage 6 seed: message templates
-- Seeds the Email/SMS templates the app sends (order lifecycle + auth).
-- Content is admin-editable later. Tables already exist (migration 011).
-- Idempotent via the UNIQUE (TenantId, Code, Channel) key + INSERT IGNORE.
-- Tokens: {{CustomerName}} {{OrderNumber}} {{OrderTotal}} {{Status}}
--         {{Courier}} {{TrackingNumber}} {{OtpCode}} {{StoreName}}
-- =====================================================================

INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `IsActive`) VALUES
(1, 'OrderConfirmation', 'Email', 'Your {{StoreName}} order {{OrderNumber}} is confirmed',
 '<p>Hi {{CustomerName}},</p><p>Thanks for your order! Your order <b>{{OrderNumber}}</b> for <b>{{OrderTotal}}</b> is confirmed and being processed.</p><p>We''ll email you again when it ships.</p><p>— {{StoreName}}</p>', 1),

(1, 'OrderStatusUpdate', 'Email', 'Update on your order {{OrderNumber}}',
 '<p>Hi {{CustomerName}},</p><p>Your order <b>{{OrderNumber}}</b> is now <b>{{Status}}</b>.</p><p>— {{StoreName}}</p>', 1),

(1, 'OrderShipped', 'Email', 'Your order {{OrderNumber}} has shipped',
 '<p>Hi {{CustomerName}},</p><p>Good news — your order <b>{{OrderNumber}}</b> has shipped via <b>{{Courier}}</b>.</p><p>Tracking number: <b>{{TrackingNumber}}</b></p><p>— {{StoreName}}</p>', 1),

(1, 'OrderCancelled', 'Email', 'Your order {{OrderNumber}} was cancelled',
 '<p>Hi {{CustomerName}},</p><p>Your order <b>{{OrderNumber}}</b> has been cancelled. If you paid, a refund has been initiated to your original payment method.</p><p>— {{StoreName}}</p>', 1),

(1, 'EmailVerification', 'Email', 'Verify your {{StoreName}} email',
 '<p>Hi {{CustomerName}},</p><p>Your verification code is <b>{{OtpCode}}</b>. It expires shortly.</p><p>If you didn''t request this, you can ignore this email.</p><p>— {{StoreName}}</p>', 1),

(1, 'PasswordReset', 'Email', 'Reset your {{StoreName}} password',
 '<p>Hi {{CustomerName}},</p><p>Your password reset code is <b>{{OtpCode}}</b>. It expires shortly.</p><p>If you didn''t request this, please ignore this email.</p><p>— {{StoreName}}</p>', 1),

(1, 'OrderConfirmation', 'SMS', NULL,
 'Hi {{CustomerName}}, your {{StoreName}} order {{OrderNumber}} ({{OrderTotal}}) is confirmed. We''ll text you when it ships.', 1),

(1, 'OrderShipped', 'SMS', NULL,
 'Your {{StoreName}} order {{OrderNumber}} shipped via {{Courier}}. Track: {{TrackingNumber}}', 1),

(1, 'OrderCancelled', 'SMS', NULL,
 'Your {{StoreName}} order {{OrderNumber}} was cancelled. Any payment will be refunded.', 1);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '024_notification_templates.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '024_notification_templates.sql');
