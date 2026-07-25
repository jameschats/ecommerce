-- ---------------------------------------------------------------------------
-- 033_email_settings.sql — SMTP configuration in the database
--
-- See documents/stages-v2/design.md §9.2–9.3. Brevo is used through its SMTP relay, so
-- going Live is configuration rather than code — but only if that configuration lives
-- somewhere an admin can reach. Hence the database rather than appsettings.json.
--
-- ⚠️ Email.SmtpPassword is stored in plain text for now. The design calls for encryption
-- at rest, which needs persisted ASP.NET Data Protection keys — the API currently logs
-- "Using an in-memory repository. Keys will not be persisted", so anything encrypted
-- today would be unreadable after the next restart. Until that is fixed, the mitigations
-- are: MySQL listens on 127.0.0.1 only, the API masks the value on read, and a Brevo SMTP
-- key can be revoked and reissued from their dashboard without touching the account.
-- ---------------------------------------------------------------------------

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT * FROM (
  SELECT 'Email.SmtpHost'     AS k, 'smtp-relay.brevo.com' AS v, NOW() AS c UNION ALL
  SELECT 'Email.SmtpPort',         '587',                       NOW()      UNION ALL
  SELECT 'Email.SmtpUsername',     '',                          NOW()      UNION ALL
  SELECT 'Email.SmtpPassword',     '',                          NOW()      UNION ALL
  SELECT 'Email.FromAddress',      '',                          NOW()      UNION ALL
  SELECT 'Email.FromName',         'DailyCalendarShop',         NOW()      UNION ALL
  SELECT 'Email.AdminNotifyTo',    '',                          NOW()
) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k
);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '033_email_settings.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '033_email_settings.sql');
