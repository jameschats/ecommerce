-- ---------------------------------------------------------------------------
-- 049_staff_roles.sql — staff roles, and the permission that was missing
--
-- The RBAC schema has been complete since 002: Roles, Permissions, RolePermissions,
-- UserRoles, and 18 seeded permissions grouped by module. Only two roles were ever
-- created — Admin and Customer — and the permissions, though issued into the JWT as a
-- "perm" claim, were never checked. So access was binary: staff or shopper.
--
-- These three roles are built entirely from permissions that already exist, except for
-- payments, which had none. IsSystem = 0 so they stay editable from the access matrix,
-- unlike Admin and Customer which must not be editable into a lockout.
-- ---------------------------------------------------------------------------

-- 1) The permission the seed was missing ----------------------------------------------
INSERT INTO `Permissions` (`Code`, `Name`, `Module`)
SELECT 'payment.verify', 'Verify payments', 'Payments'
WHERE NOT EXISTS (SELECT 1 FROM `Permissions` WHERE `Code` = 'payment.verify');

-- Admin holds everything, including anything added later.
INSERT INTO `RolePermissions` (`RoleId`, `PermissionId`)
SELECT r.`RoleId`, p.`PermissionId`
FROM `Roles` r
CROSS JOIN `Permissions` p
WHERE r.`TenantId` = 1 AND r.`NormalizedName` = 'ADMIN'
  AND NOT EXISTS (
    SELECT 1 FROM `RolePermissions` rp
    WHERE rp.`RoleId` = r.`RoleId` AND rp.`PermissionId` = p.`PermissionId`
  );

-- 2) The staff roles -------------------------------------------------------------------
INSERT INTO `Roles` (`TenantId`, `Name`, `NormalizedName`, `Description`, `IsSystem`)
SELECT * FROM (
  SELECT 1 AS t, 'Staff'             AS n, 'STAFF'             AS nn, 'Orders, payments and stock'        AS d, 0 AS s UNION ALL
  SELECT 1, 'Accountant',                  'ACCOUNTANT',            'Read-only orders, payments, reports',     0 UNION ALL
  SELECT 1, 'Catalogue manager',           'CATALOGUEMANAGER',      'Products, media and coupons',            0
) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Roles` r WHERE r.`TenantId` = 1 AND r.`NormalizedName` = seed.nn
);

-- 3) What each role may do -------------------------------------------------------------
--    Written as (role, permission) pairs so the grants read like the access matrix does.
INSERT INTO `RolePermissions` (`RoleId`, `PermissionId`)
SELECT r.`RoleId`, p.`PermissionId`
FROM (
  SELECT 'STAFF' AS role, 'order.view'       AS perm UNION ALL
  SELECT 'STAFF',              'order.manage'      UNION ALL
  SELECT 'STAFF',              'inventory.view'    UNION ALL
  SELECT 'STAFF',              'inventory.manage'  UNION ALL
  SELECT 'STAFF',              'payment.verify'    UNION ALL
  SELECT 'STAFF',              'customer.view'     UNION ALL

  SELECT 'ACCOUNTANT',         'order.view'        UNION ALL
  SELECT 'ACCOUNTANT',         'payment.verify'    UNION ALL
  SELECT 'ACCOUNTANT',         'report.view'       UNION ALL

  SELECT 'CATALOGUEMANAGER',   'catalog.view'      UNION ALL
  SELECT 'CATALOGUEMANAGER',   'catalog.manage'    UNION ALL
  SELECT 'CATALOGUEMANAGER',   'inventory.view'    UNION ALL
  SELECT 'CATALOGUEMANAGER',   'media.manage'      UNION ALL
  SELECT 'CATALOGUEMANAGER',   'import.manage'     UNION ALL
  SELECT 'CATALOGUEMANAGER',   'coupon.manage'     UNION ALL
  SELECT 'CATALOGUEMANAGER',   'review.moderate'   UNION ALL
  SELECT 'CATALOGUEMANAGER',   'cms.manage'
) AS grants
JOIN `Roles` r ON r.`TenantId` = 1 AND r.`NormalizedName` = grants.role
JOIN `Permissions` p ON p.`Code` = grants.perm
WHERE NOT EXISTS (
  SELECT 1 FROM `RolePermissions` rp
  WHERE rp.`RoleId` = r.`RoleId` AND rp.`PermissionId` = p.`PermissionId`
);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '049_staff_roles.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '049_staff_roles.sql');
