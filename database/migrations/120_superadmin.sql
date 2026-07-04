-- =====================================================================
-- 120_superadmin.sql  —  V2-3: super-admin (platform owner) foundation.
-- SuperAdmin role + governance columns on Tenants + platform audit log.
-- Additive (V2 band 120-129).
-- =====================================================================

-- Global SuperAdmin role (platform owner; cross-tenant). Not tenant-scoped.
INSERT INTO `Roles` (`TenantId`, `Name`, `NormalizedName`, `Description`, `IsSystem`, `CreatedAt`)
SELECT 1, 'SuperAdmin', 'SUPERADMIN', 'Platform owner — cross-tenant governance', 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Roles` WHERE `NormalizedName` = 'SUPERADMIN');

-- Grant it to the seeded platform admin (admin@ecommerce.local).
INSERT INTO `UserRoles` (`UserId`, `RoleId`)
SELECT u.`UserId`, r.`RoleId`
FROM `Users` u CROSS JOIN `Roles` r
WHERE u.`NormalizedEmail` = 'ADMIN@ECOMMERCE.LOCAL' AND r.`NormalizedName` = 'SUPERADMIN'
  AND NOT EXISTS (SELECT 1 FROM `UserRoles` ur WHERE ur.`UserId` = u.`UserId` AND ur.`RoleId` = r.`RoleId`);

-- Merchant standing (governance): Good | Trusted | Watch | Flagged | Blacklisted.
ALTER TABLE `Tenants`
    ADD COLUMN `Standing`          VARCHAR(20)  NOT NULL DEFAULT 'Good' AFTER `IsActive`,
    ADD COLUMN `StandingReason`    VARCHAR(500) NULL AFTER `Standing`,
    ADD COLUMN `StandingUpdatedAt` DATETIME     NULL AFTER `StandingReason`;

-- Every super-admin view/action on a tenant is logged here (design-v2 V2-10).
CREATE TABLE IF NOT EXISTS `PlatformAccessLog` (
    `PlatformAccessLogId` BIGINT       NOT NULL AUTO_INCREMENT,
    `AdminUserId`         BIGINT       NOT NULL,
    `TenantId`            BIGINT       NULL,
    `Action`              VARCHAR(80)  NOT NULL,       -- ViewTenant | SetStanding | Suspend | Impersonate | ...
    `Detail`              VARCHAR(500) NULL,
    `CreatedAt`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`PlatformAccessLogId`),
    KEY `ix_platformaccess_tenant` (`TenantId`),
    KEY `ix_platformaccess_admin` (`AdminUserId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '120_superadmin.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '120_superadmin.sql');
