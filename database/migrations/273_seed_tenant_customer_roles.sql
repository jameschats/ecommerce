-- =====================================================================
-- 273_seed_tenant_customer_roles.sql
-- Roles are tenant-scoped, but onboarding never seeded them for stores past the default tenant.
-- So AuthService.AssignRoleAsync (which requires a role with the user's own TenantId) silently
-- no-op'd, and every shopper on those stores got NO role — making them invisible in the admin
-- Customers list (CustomerAdminService filters by CUSTOMER-role membership).
-- This: (1) seeds a CUSTOMER role for every tenant that has users but lacks one, then (2) grants it
-- to every active user currently holding no role at all (the affected shoppers). Store admins keep
-- their ADMIN role (they already have one, so the NOT-EXISTS guard skips them). Idempotent. Additive.
-- Apply with SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci (deploy loop does this).
-- =====================================================================

INSERT INTO `Roles` (`TenantId`, `Name`, `NormalizedName`, `IsSystem`, `CreatedAt`)
SELECT DISTINCT u.`TenantId`, 'Customer', 'CUSTOMER', 1, UTC_TIMESTAMP()
FROM `Users` u
WHERE NOT EXISTS (
    SELECT 1 FROM `Roles` r WHERE r.`TenantId` = u.`TenantId` AND r.`NormalizedName` = 'CUSTOMER'
);

INSERT INTO `UserRoles` (`UserId`, `RoleId`)
SELECT u.`UserId`, r.`RoleId`
FROM `Users` u
JOIN `Roles` r ON r.`TenantId` = u.`TenantId` AND r.`NormalizedName` = 'CUSTOMER'
WHERE u.`IsDeleted` = 0
  AND NOT EXISTS (SELECT 1 FROM `UserRoles` ur WHERE ur.`UserId` = u.`UserId`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '273_seed_tenant_customer_roles.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '273_seed_tenant_customer_roles.sql');
