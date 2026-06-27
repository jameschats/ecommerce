-- =====================================================================
-- 002_core.sql  —  Core domain: tenancy, identity, RBAC
-- Tables: Tenants, Permissions, Roles, Users, UserRoles, RolePermissions
-- TenantId is present from Day 1 (defaults to 1) for V2 multi-tenant.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Tenants` (
    `TenantId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `Name`       VARCHAR(150) NOT NULL,
    `Code`       VARCHAR(50)  NOT NULL,
    `IsActive`   TINYINT(1)   NOT NULL DEFAULT 1,
    `CreatedAt`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`  DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`TenantId`),
    UNIQUE KEY `uq_tenants_code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Permissions` (
    `PermissionId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `Code`         VARCHAR(100) NOT NULL,
    `Name`         VARCHAR(150) NOT NULL,
    `Module`       VARCHAR(50)  NULL,
    `Description`  VARCHAR(255) NULL,
    `CreatedAt`    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`PermissionId`),
    UNIQUE KEY `uq_permissions_code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Roles` (
    `RoleId`         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`           VARCHAR(100) NOT NULL,
    `NormalizedName` VARCHAR(100) NOT NULL,
    `Description`    VARCHAR(255) NULL,
    `IsSystem`       TINYINT(1)   NOT NULL DEFAULT 0,
    `CreatedAt`      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`      DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`RoleId`),
    UNIQUE KEY `uq_roles_tenant_name` (`TenantId`, `NormalizedName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Users` (
    `UserId`          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Email`           VARCHAR(256) NOT NULL,
    `NormalizedEmail` VARCHAR(256) NOT NULL,
    `PasswordHash`    VARCHAR(255) NOT NULL,
    `FullName`        VARCHAR(150) NULL,
    `PhoneNumber`     VARCHAR(20)  NULL,
    `IsEmailVerified` TINYINT(1)   NOT NULL DEFAULT 0,
    `EmailVerifiedAt` DATETIME     NULL,
    `IsActive`        TINYINT(1)   NOT NULL DEFAULT 1,
    `IsDeleted`       TINYINT(1)   NOT NULL DEFAULT 0,
    `LastLoginAt`     DATETIME     NULL,
    `CreatedAt`       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`UserId`),
    UNIQUE KEY `uq_users_tenant_email` (`TenantId`, `NormalizedEmail`),
    KEY `ix_users_phone` (`PhoneNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `UserRoles` (
    `UserId` BIGINT UNSIGNED NOT NULL,
    `RoleId` BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (`UserId`, `RoleId`),
    KEY `ix_userroles_role` (`RoleId`),
    CONSTRAINT `fk_userroles_user` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`) ON DELETE CASCADE,
    CONSTRAINT `fk_userroles_role` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`RoleId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `RolePermissions` (
    `RoleId`       BIGINT UNSIGNED NOT NULL,
    `PermissionId` BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (`RoleId`, `PermissionId`),
    KEY `ix_rolepermissions_permission` (`PermissionId`),
    CONSTRAINT `fk_rolepermissions_role` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`RoleId`) ON DELETE CASCADE,
    CONSTRAINT `fk_rolepermissions_permission` FOREIGN KEY (`PermissionId`) REFERENCES `Permissions` (`PermissionId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '002_core.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '002_core.sql');
