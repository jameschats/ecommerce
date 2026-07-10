-- =====================================================================
-- 168_custom_domain.sql  —  V2 Merchant-Admin M8c: custom domain connect.
-- A merchant points their own domain (www.brand.com) at the platform. We store
-- the domain + a verification token + verified flag; resolution maps the domain
-- to the tenant. `CustomDomain` already exists on Tenants (added in V2-0 band);
-- this adds the verification state. Additive (V2 band 160-169).
-- =====================================================================

-- MySQL 8: ADD COLUMN IF NOT EXISTS is not supported; guard via information_schema.
SET @db := DATABASE();

SET @sql := IF(
    NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS
               WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Tenants' AND COLUMN_NAME = 'CustomDomainVerified'),
    'ALTER TABLE `Tenants` ADD COLUMN `CustomDomainVerified` TINYINT(1) NOT NULL DEFAULT 0',
    'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
    NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS
               WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Tenants' AND COLUMN_NAME = 'CustomDomainToken'),
    'ALTER TABLE `Tenants` ADD COLUMN `CustomDomainToken` VARCHAR(64) NULL',
    'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Unique domain across tenants (NULLs allowed multiple times in MySQL).
SET @sql := IF(
    NOT EXISTS(SELECT 1 FROM information_schema.STATISTICS
               WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Tenants' AND INDEX_NAME = 'uq_tenants_customdomain'),
    'ALTER TABLE `Tenants` ADD UNIQUE KEY `uq_tenants_customdomain` (`CustomDomain`)',
    'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '168_custom_domain.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '168_custom_domain.sql');
