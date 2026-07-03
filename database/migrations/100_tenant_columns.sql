-- =====================================================================
-- 100_tenant_columns.sql  —  V2-0: extend the Tenants table (multi-tenant SaaS)
--
-- FIRST V2 migration. V1 scripts (001-099) are frozen. Additive only — the
-- existing bigint TenantId PK is kept (NOT switched to a GUID); see
-- documents/design-v2.md §3. Backward-compatible: the existing default tenant
-- (TenantId = 1) keeps working; apex-domain traffic resolves to it.
-- =====================================================================

ALTER TABLE `Tenants`
    ADD COLUMN `Slug`         VARCHAR(80)  NULL AFTER `Code`,
    ADD COLUMN `DisplayName`  VARCHAR(200) NULL AFTER `Name`,
    ADD COLUMN `CustomDomain` VARCHAR(255) NULL AFTER `Slug`,   -- post-GA
    ADD COLUMN `PlanId`       INT          NULL AFTER `CustomDomain`,
    ADD COLUMN `TrialEndsAt`  DATETIME     NULL AFTER `PlanId`,
    ADD COLUMN `SuspendedAt`  DATETIME     NULL AFTER `TrialEndsAt`;

-- Backfill Slug from Code (subdomain), DisplayName from Name, for existing rows.
UPDATE `Tenants` SET `Slug` = LOWER(`Code`) WHERE `Slug` IS NULL;
UPDATE `Tenants` SET `DisplayName` = `Name` WHERE `DisplayName` IS NULL;

-- Slug is the subdomain lookup key — must be unique.
CREATE UNIQUE INDEX `ux_tenants_slug` ON `Tenants` (`Slug`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '100_tenant_columns.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '100_tenant_columns.sql');
