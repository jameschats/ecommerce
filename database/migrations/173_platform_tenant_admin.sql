-- =====================================================================
-- 173_platform_tenant_admin.sql  —  Super-admin SA2: tenant lifecycle controls.
-- Platform tags + soft-offboard state on Tenants, plus append-only internal
-- notes. Plan-change / trial use existing Tenants.PlanId/TrialEndsAt +
-- TenantSubscriptions. Additive (V2 band 170+). Run-once (recorded below).
-- =====================================================================

ALTER TABLE `Tenants` ADD COLUMN `PlatformTags` VARCHAR(500) NULL;
ALTER TABLE `Tenants` ADD COLUMN `OffboardedAt`  DATETIME     NULL;

CREATE TABLE IF NOT EXISTS `TenantNotes` (
    `TenantNoteId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`     BIGINT UNSIGNED NOT NULL,
    `AdminUserId`  BIGINT UNSIGNED NOT NULL,
    `Note`         VARCHAR(2000) NOT NULL,
    `CreatedAt`    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`TenantNoteId`),
    KEY `ix_tenantnotes_tenant` (`TenantId`, `TenantNoteId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '173_platform_tenant_admin.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '173_platform_tenant_admin.sql');
