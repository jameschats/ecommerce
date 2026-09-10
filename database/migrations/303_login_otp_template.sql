-- =====================================================================
-- 303_login_otp_template.sql
--
-- Mobile-OTP login (OtpService, purpose "Login") only ever sent a raw
-- SMS with no template and no fallback. It now falls back to email when
-- the admin disables the SMS channel (Settings.ChannelSMSEnabled=false
-- — see NotificationChannelSettings, added alongside this migration) —
-- e.g. while SMS is blocked on MSG91 DLT registration. That fallback
-- needs a real Email template to render into, same as PasswordReset/
-- EmailVerification. Seeds it on tenant 1 and backfills every other
-- tenant, same pattern as migrations 252/300.
-- Idempotent: UNIQUE (TenantId, Code, Channel) + INSERT IGNORE / NOT EXISTS.
-- Tokens: {{OtpCode}} {{CustomerName}} {{StoreName}}
-- =====================================================================

INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `IsActive`) VALUES
(1, 'LoginOtp', 'Email', 'Your {{StoreName}} sign-in code',
 '<p>Hi {{CustomerName}},</p><p>Your sign-in code is <b>{{OtpCode}}</b>. It expires shortly.</p><p>If you didn''t request this, you can ignore this email.</p><p>— {{StoreName}}</p>', 1);

INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `IsActive`)
SELECT t.`TenantId`, s.`Code`, s.`Channel`, s.`Subject`, s.`Body`, s.`IsActive`
  FROM `Tenants` t
  CROSS JOIN `NotificationTemplates` s
 WHERE s.`TenantId` = 1
   AND s.`Code` = 'LoginOtp'
   AND t.`TenantId` <> 1
   AND NOT EXISTS (
        SELECT 1 FROM `NotificationTemplates` x
         WHERE x.`TenantId` = t.`TenantId` AND x.`Code` = s.`Code` AND x.`Channel` = s.`Channel`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '303_login_otp_template.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '303_login_otp_template.sql');
