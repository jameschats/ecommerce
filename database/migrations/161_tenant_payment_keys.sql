-- =====================================================================
-- 161_tenant_payment_keys.sql  —  V2 Merchant-Admin M4a: per-tenant payments.
-- Each merchant configures their OWN payment provider + keys (payments settle to
-- their account). Extends the existing TenantPaymentAccounts table (migration 110).
-- The secret is stored as ASP.NET Data-Protection ciphertext (never plaintext).
-- Additive (V2 band 160-169).
-- =====================================================================

ALTER TABLE `TenantPaymentAccounts`
    ADD COLUMN `RazorpayKeyId`     VARCHAR(120)  NULL AFTER `Provider`,
    ADD COLUMN `RazorpayKeySecret` VARCHAR(1000) NULL AFTER `RazorpayKeyId`,   -- data-protected ciphertext
    ADD COLUMN `IsEnabled`         TINYINT(1)    NOT NULL DEFAULT 0 AFTER `IsVerified`,
    ADD COLUMN `UpdatedAt`         DATETIME      NULL;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '161_tenant_payment_keys.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '161_tenant_payment_keys.sql');
