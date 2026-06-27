-- =====================================================================
-- 016_auth_provider_order.sql
-- Make Mobile OTP the primary sign-in method (display order).
-- Mobile-first auth, matching Indian e-commerce norms (Flipkart/Myntra/Nykaa).
-- =====================================================================

UPDATE `AuthProviders` SET `DisplayOrder` = 1, `UpdatedAt` = CURRENT_TIMESTAMP
WHERE `TenantId` = 1 AND `Provider` = 'MobileOtp';

UPDATE `AuthProviders` SET `DisplayOrder` = 2, `UpdatedAt` = CURRENT_TIMESTAMP
WHERE `TenantId` = 1 AND `Provider` = 'EmailPassword';

UPDATE `AuthProviders` SET `DisplayOrder` = 3, `UpdatedAt` = CURRENT_TIMESTAMP
WHERE `TenantId` = 1 AND `Provider` = 'Google';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '016_auth_provider_order.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '016_auth_provider_order.sql');
