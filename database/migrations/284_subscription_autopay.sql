-- =====================================================================
-- 284_subscription_autopay.sql  —  Billing P2: recurring auto-debit (Razorpay Subscriptions).
-- Stores the mandate/subscription state so a merchant sets up auto-pay once and is then charged
-- automatically each cycle. Additive + idempotent.
-- =====================================================================

-- Cached Razorpay Plan id per platform Plan (created once, reused for every subscription on that plan).
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Plans' AND COLUMN_NAME = 'RazorpayPlanId');
SET @s := IF(@c = 0, 'ALTER TABLE `Plans` ADD COLUMN `RazorpayPlanId` VARCHAR(64) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- Per-subscription mandate state.
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantSubscriptions' AND COLUMN_NAME = 'RazorpayCustomerId');
SET @s := IF(@c = 0, 'ALTER TABLE `TenantSubscriptions` ADD COLUMN `RazorpayCustomerId` VARCHAR(64) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantSubscriptions' AND COLUMN_NAME = 'MandateStatus');
SET @s := IF(@c = 0, "ALTER TABLE `TenantSubscriptions` ADD COLUMN `MandateStatus` VARCHAR(20) NOT NULL DEFAULT 'none'", 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantSubscriptions' AND COLUMN_NAME = 'PaymentMethodSummary');
SET @s := IF(@c = 0, 'ALTER TABLE `TenantSubscriptions` ADD COLUMN `PaymentMethodSummary` VARCHAR(100) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantSubscriptions' AND COLUMN_NAME = 'NextChargeAt');
SET @s := IF(@c = 0, 'ALTER TABLE `TenantSubscriptions` ADD COLUMN `NextChargeAt` DATETIME NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantSubscriptions' AND COLUMN_NAME = 'CancelAtPeriodEnd');
SET @s := IF(@c = 0, 'ALTER TABLE `TenantSubscriptions` ADD COLUMN `CancelAtPeriodEnd` TINYINT(1) NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '284_subscription_autopay.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '284_subscription_autopay.sql');
