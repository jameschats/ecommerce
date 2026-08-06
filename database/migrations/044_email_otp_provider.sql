-- ---------------------------------------------------------------------------
-- 044_email_otp_provider.sql — email OTP as a first-class sign-in method
--
-- The endpoints have existed since 015 and the quick-order checkout gate has used
-- them all along, but email OTP was never a row in AuthProviders. So it could not
-- be listed, ordered or toggled from admin, and the login page — which renders
-- itself from the enabled providers — had no way to offer it. /login has only ever
-- shown "Email & Password" and "Mobile OTP".
--
-- That matters now for one specific reason: accounts created by mobile OTP have no
-- password. Turning Mobile OTP off in production without this would leave those
-- customers with nothing to sign in with, locked out of their own order history.
-- Email OTP is the way back in, and it is free on Brevo where SMS costs per message.
--
-- DisplayOrder 1 makes it the default choice, ahead of Mobile OTP.
-- ---------------------------------------------------------------------------

INSERT INTO `AuthProviders` (`TenantId`, `Provider`, `IsEnabled`, `AllowRegistration`, `DisplayName`, `DisplayOrder`)
SELECT 1, 'EmailOtp', 1, 1, 'Email OTP', 1
WHERE NOT EXISTS (
  SELECT 1 FROM `AuthProviders` p WHERE p.`TenantId` = 1 AND p.`Provider` = 'EmailOtp'
);

-- Push the existing methods down so the new default sits at the top.
UPDATE `AuthProviders` SET `DisplayOrder` = 2, `UpdatedAt` = CURRENT_TIMESTAMP
WHERE `TenantId` = 1 AND `Provider` = 'MobileOtp';

UPDATE `AuthProviders` SET `DisplayOrder` = 3, `UpdatedAt` = CURRENT_TIMESTAMP
WHERE `TenantId` = 1 AND `Provider` = 'EmailPassword';

UPDATE `AuthProviders` SET `DisplayOrder` = 4, `UpdatedAt` = CURRENT_TIMESTAMP
WHERE `TenantId` = 1 AND `Provider` = 'Google';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '044_email_otp_provider.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '044_email_otp_provider.sql');
