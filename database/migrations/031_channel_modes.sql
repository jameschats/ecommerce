-- ---------------------------------------------------------------------------
-- 031_channel_modes.sql — Mock/Live switch per notification channel
--
-- See documents/stages-v2/design.md §9. Every channel runs in Mock or Live, chosen by an
-- admin from the database rather than appsettings.json, because the person who needs to
-- flip it cannot edit a JSON file on a server.
--
-- Mock and Live run the SAME code path — only delivery differs. In Mock the OTP is the
-- fixed Channels.OtpMockCode instead of a random one, and nothing is handed to a paid
-- provider. Verification, expiry, resend cooldown and attempt limits are untouched.
--
-- Defaults are Mock so a fresh environment is testable before any paid account exists.
-- Flipping SMS and Email to Live is a blocking item on the go-live checklist.
-- ---------------------------------------------------------------------------

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT * FROM (
  SELECT 'Channels.SmsMode'      AS k, 'Mock'   AS v, NOW() AS c UNION ALL
  SELECT 'Channels.EmailMode',        'Mock',        NOW()      UNION ALL
  SELECT 'Channels.WhatsAppMode',     'Mock',        NOW()      UNION ALL
  SELECT 'Channels.OtpMockCode',      '000000',      NOW()
) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k
);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '031_channel_modes.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '031_channel_modes.sql');
