-- =====================================================================
-- 111_subscription_grace.sql  —  V2-1 Phase 3: grace-period tracking on
-- subscriptions. Additive. When a period ends unpaid the subscription goes
-- PastDue with a GraceEndsAt; after grace the tenant is Suspended.
-- =====================================================================

ALTER TABLE `TenantSubscriptions`
    ADD COLUMN `GraceEndsAt` DATETIME NULL AFTER `CurrentPeriodEnd`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '111_subscription_grace.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '111_subscription_grace.sql');
