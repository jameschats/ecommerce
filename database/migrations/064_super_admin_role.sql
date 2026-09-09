-- ---------------------------------------------------------------------------
-- 064_super_admin_role.sql — Super Admin role, and a trimmed-down Admin
--
-- Admin has held every permission since 014_seed.sql (a straight cross-join).
-- That's now too broad: Admin should keep Orders/Products/Customers/Discounts/
-- Marketing/Analytics plus just the Notifications screen, and lose Online
-- Store (CMS/Theme) and the rest of Settings (Store/Shop settings, message
-- templates, sign-in methods, Go-live/Data-reset) and Users & Roles.
--
-- Super Admin is the new "everything" role — IsSystem = 1, like Admin, so
-- neither can be edited into a lockout from the access matrix UI.
-- ---------------------------------------------------------------------------

-- 1) The permission Admin's remaining Notifications access needs -----------------------
--    Narrower than settings.manage: just the Notifications screen.
INSERT INTO `Permissions` (`Code`, `Name`, `Module`)
SELECT 'settings.notifications', 'View notifications', 'Settings'
WHERE NOT EXISTS (SELECT 1 FROM `Permissions` WHERE `Code` = 'settings.notifications');

-- 2) Super Admin — the new full-access role ---------------------------------------------
INSERT INTO `Roles` (`TenantId`, `Name`, `NormalizedName`, `Description`, `IsSystem`)
SELECT 1, 'Super Admin', 'SUPERADMIN', 'Full access to every admin screen', 1
WHERE NOT EXISTS (SELECT 1 FROM `Roles` WHERE `TenantId` = 1 AND `NormalizedName` = 'SUPERADMIN');

-- Holds everything, including anything added later — same pattern as Admin's own grant.
INSERT INTO `RolePermissions` (`RoleId`, `PermissionId`)
SELECT r.`RoleId`, p.`PermissionId`
FROM `Roles` r
CROSS JOIN `Permissions` p
WHERE r.`TenantId` = 1 AND r.`NormalizedName` = 'SUPERADMIN'
  AND NOT EXISTS (
    SELECT 1 FROM `RolePermissions` rp
    WHERE rp.`RoleId` = r.`RoleId` AND rp.`PermissionId` = p.`PermissionId`
  );

-- 3) Admin keeps Notifications, loses Online Store + the rest of Settings + Users & Roles
INSERT INTO `RolePermissions` (`RoleId`, `PermissionId`)
SELECT r.`RoleId`, p.`PermissionId`
FROM `Roles` r
JOIN `Permissions` p ON p.`Code` = 'settings.notifications'
WHERE r.`TenantId` = 1 AND r.`NormalizedName` = 'ADMIN'
  AND NOT EXISTS (
    SELECT 1 FROM `RolePermissions` rp
    WHERE rp.`RoleId` = r.`RoleId` AND rp.`PermissionId` = p.`PermissionId`
  );

DELETE rp FROM `RolePermissions` rp
JOIN `Roles` r ON r.`RoleId` = rp.`RoleId`
JOIN `Permissions` p ON p.`PermissionId` = rp.`PermissionId`
WHERE r.`TenantId` = 1 AND r.`NormalizedName` = 'ADMIN'
  AND p.`Code` IN ('cms.manage', 'theme.manage', 'settings.manage', 'user.manage', 'role.manage');

-- 4) admin@ecommerce.local moves from Admin to Super Admin ------------------------------
INSERT INTO `UserRoles` (`UserId`, `RoleId`)
SELECT u.`UserId`, r.`RoleId`
FROM `Users` u
JOIN `Roles` r ON r.`TenantId` = 1 AND r.`NormalizedName` = 'SUPERADMIN'
WHERE u.`TenantId` = 1 AND u.`Email` = 'admin@ecommerce.local'
  AND NOT EXISTS (SELECT 1 FROM `UserRoles` ur WHERE ur.`UserId` = u.`UserId` AND ur.`RoleId` = r.`RoleId`);

DELETE ur FROM `UserRoles` ur
JOIN `Users` u ON u.`UserId` = ur.`UserId`
JOIN `Roles` r ON r.`RoleId` = ur.`RoleId`
WHERE u.`TenantId` = 1 AND u.`Email` = 'admin@ecommerce.local' AND r.`NormalizedName` = 'ADMIN';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '064_super_admin_role.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '064_super_admin_role.sql');
