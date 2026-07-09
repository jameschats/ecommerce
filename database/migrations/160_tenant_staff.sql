-- =====================================================================
-- 160_tenant_staff.sql  —  V2 Merchant-Admin M3: Staff & roles.
-- Staff = a User in the ADMIN role (panel access) PLUS a per-tenant access
-- level. This table holds the level + status + invite metadata; the ADMIN
-- role itself still gates admin controllers, so existing auth is untouched.
-- Access levels: Owner | Admin | Staff | Viewer.  Status: Active | Disabled.
-- Additive (V2 band 160-169).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `TenantStaff` (
    `TenantStaffId`   BIGINT      NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT      NOT NULL DEFAULT 1,
    `UserId`          BIGINT      NOT NULL,
    `AccessLevel`     VARCHAR(20) NOT NULL DEFAULT 'Staff',   -- Owner | Admin | Staff | Viewer
    `Status`          VARCHAR(20) NOT NULL DEFAULT 'Active',  -- Active | Disabled
    `InvitedByUserId` BIGINT      NULL,
    `CreatedAt`       DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME    NULL,
    PRIMARY KEY (`TenantStaffId`),
    UNIQUE KEY `uq_tenantstaff_user` (`TenantId`, `UserId`),
    KEY `ix_tenantstaff_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Backfill existing admins: the earliest ADMIN user per tenant becomes Owner, the rest Admin.
INSERT INTO `TenantStaff` (`TenantId`, `UserId`, `AccessLevel`, `Status`, `CreatedAt`)
SELECT u.`TenantId`, u.`UserId`,
    CASE WHEN u.`UserId` = (
        SELECT MIN(u2.`UserId`) FROM `Users` u2
        JOIN `UserRoles` ur2 ON ur2.`UserId` = u2.`UserId`
        JOIN `Roles` r2 ON r2.`RoleId` = ur2.`RoleId`
        WHERE r2.`NormalizedName` = 'ADMIN' AND u2.`TenantId` = u.`TenantId` AND u2.`IsDeleted` = 0
    ) THEN 'Owner' ELSE 'Admin' END,
    'Active', NOW()
FROM `Users` u
JOIN `UserRoles` ur ON ur.`UserId` = u.`UserId`
JOIN `Roles` r ON r.`RoleId` = ur.`RoleId`
WHERE r.`NormalizedName` = 'ADMIN' AND u.`IsDeleted` = 0
  AND NOT EXISTS (SELECT 1 FROM `TenantStaff` ts WHERE ts.`UserId` = u.`UserId`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '160_tenant_staff.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '160_tenant_staff.sql');
