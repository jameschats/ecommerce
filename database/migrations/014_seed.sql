-- =====================================================================
-- 014_seed.sql  —  Baseline seed data (idempotent)
-- Default tenant, RBAC roles/permissions, admin user, GST tax rates,
-- a default shipping method, platform settings, default theme, home page.
-- =====================================================================

-- Tenant --------------------------------------------------------------
INSERT IGNORE INTO `Tenants` (`TenantId`, `Name`, `Code`, `IsActive`)
VALUES (1, 'Default Store', 'default', 1);

-- Roles ---------------------------------------------------------------
INSERT IGNORE INTO `Roles` (`TenantId`, `Name`, `NormalizedName`, `Description`, `IsSystem`)
VALUES
    (1, 'Admin',    'ADMIN',    'Full administrative access', 1),
    (1, 'Customer', 'CUSTOMER', 'Storefront customer',        1);

-- Permissions ---------------------------------------------------------
INSERT IGNORE INTO `Permissions` (`Code`, `Name`, `Module`) VALUES
    ('catalog.view',     'View catalog',        'Catalog'),
    ('catalog.manage',   'Manage catalog',      'Catalog'),
    ('inventory.view',   'View inventory',      'Inventory'),
    ('inventory.manage', 'Manage inventory',    'Inventory'),
    ('order.view',       'View orders',         'Orders'),
    ('order.manage',     'Manage orders',       'Orders'),
    ('customer.view',    'View customers',      'Customers'),
    ('customer.manage',  'Manage customers',    'Customers'),
    ('coupon.manage',    'Manage coupons',      'Coupons'),
    ('review.moderate',  'Moderate reviews',    'Reviews'),
    ('cms.manage',       'Manage CMS pages',    'Cms'),
    ('theme.manage',     'Manage themes',       'Theme'),
    ('settings.manage',  'Manage settings',     'Settings'),
    ('media.manage',     'Manage media',        'Media'),
    ('import.manage',    'Manage import/export','ImportExport'),
    ('report.view',      'View reports',        'Reports'),
    ('user.manage',      'Manage users',        'Identity'),
    ('role.manage',      'Manage roles',        'Identity');

-- Admin gets every permission
INSERT IGNORE INTO `RolePermissions` (`RoleId`, `PermissionId`)
SELECT r.`RoleId`, p.`PermissionId`
FROM `Roles` r
CROSS JOIN `Permissions` p
WHERE r.`TenantId` = 1 AND r.`NormalizedName` = 'ADMIN';

-- Admin user ----------------------------------------------------------
-- NOTE: PasswordHash is a placeholder. The Auth module (Stage 1) must set
-- a real hash (e.g. via ASP.NET Identity PasswordHasher / BCrypt) before login.
INSERT IGNORE INTO `Users`
    (`TenantId`, `Email`, `NormalizedEmail`, `PasswordHash`, `FullName`, `IsEmailVerified`, `IsActive`)
VALUES
    (1, 'admin@ecommerce.local', 'ADMIN@ECOMMERCE.LOCAL', 'SET_BY_AUTH_MODULE', 'Administrator', 1, 1);

INSERT IGNORE INTO `UserRoles` (`UserId`, `RoleId`)
SELECT u.`UserId`, r.`RoleId`
FROM `Users` u
JOIN `Roles` r ON r.`TenantId` = u.`TenantId` AND r.`NormalizedName` = 'ADMIN'
WHERE u.`NormalizedEmail` = 'ADMIN@ECOMMERCE.LOCAL';

-- GST tax rates -------------------------------------------------------
INSERT INTO `TaxRates` (`TenantId`, `Name`, `CgstRate`, `SgstRate`, `IgstRate`, `TotalRate`)
SELECT seed.`t`, seed.`n`, seed.`c`, seed.`s`, seed.`i`, seed.`tot` FROM (
    SELECT 1 AS `t`, 'GST 0%'  AS `n`, 0.00 AS `c`, 0.00 AS `s`, 0.00  AS `i`, 0.00  AS `tot` UNION ALL
    SELECT 1, 'GST 5%',  2.50, 2.50, 5.00,  5.00  UNION ALL
    SELECT 1, 'GST 12%', 6.00, 6.00, 12.00, 12.00 UNION ALL
    SELECT 1, 'GST 18%', 9.00, 9.00, 18.00, 18.00 UNION ALL
    SELECT 1, 'GST 28%', 14.00, 14.00, 28.00, 28.00
) AS seed
WHERE NOT EXISTS (SELECT 1 FROM `TaxRates` WHERE `TenantId` = 1 AND `Name` = seed.`n`);

-- Default shipping method --------------------------------------------
INSERT INTO `ShippingMethods` (`TenantId`, `Name`, `Description`, `RateType`, `BaseRate`, `FreeShippingThreshold`, `EstimatedDays`)
SELECT 1, 'Standard Delivery', 'Flat rate; free above the threshold', 'Flat', 50.00, 500.00, 5
WHERE NOT EXISTS (SELECT 1 FROM `ShippingMethods` WHERE `TenantId` = 1 AND `Name` = 'Standard Delivery');

-- Platform settings ---------------------------------------------------
INSERT IGNORE INTO `Settings` (`TenantId`, `SettingKey`, `SettingValue`, `DataType`, `Category`) VALUES
    (1, 'SiteName',       'My Store', 'string', 'General'),
    (1, 'CurrencyCode',   'INR',      'string', 'General'),
    (1, 'EnableReviews',  'true',     'bool',   'Features'),
    (1, 'EnableCoupons',  'true',     'bool',   'Features'),
    (1, 'EnableWishlist', 'false',    'bool',   'Features'),
    (1, 'EnableCOD',      'false',    'bool',   'Payments');

-- Default theme -------------------------------------------------------
INSERT IGNORE INTO `Themes` (`TenantId`, `Name`, `IsActive`) VALUES (1, 'Default', 1);

INSERT IGNORE INTO `ThemeSettings` (`ThemeId`, `SettingKey`, `SettingValue`)
SELECT t.`ThemeId`, s.`SettingKey`, s.`SettingValue`
FROM `Themes` t
JOIN (
    SELECT 'PrimaryColor'   AS SettingKey, '#2563eb' AS SettingValue UNION ALL
    SELECT 'SecondaryColor', '#1e293b' UNION ALL
    SELECT 'Font',           'Inter'   UNION ALL
    SELECT 'ButtonStyle',    'rounded' UNION ALL
    SELECT 'Logo',           ''
) s
WHERE t.`TenantId` = 1 AND t.`Name` = 'Default';

-- Default home page + sections ---------------------------------------
INSERT IGNORE INTO `Pages` (`TenantId`, `Title`, `Slug`, `Type`, `IsPublished`)
VALUES (1, 'Home', 'home', 'Home', 1);

INSERT INTO `PageSections` (`PageId`, `SectionType`, `Title`, `DisplayOrder`, `IsVisible`)
SELECT p.`PageId`, s.`SectionType`, s.`Title`, s.`DisplayOrder`, 1
FROM `Pages` p
JOIN (
    SELECT 'Banner'           AS SectionType, 'Hero Banner'    AS Title, 1 AS DisplayOrder UNION ALL
    SELECT 'Categories',       'Shop by Category', 2 UNION ALL
    SELECT 'FeaturedProducts', 'Featured Products', 3 UNION ALL
    SELECT 'NewArrivals',      'New Arrivals',      4 UNION ALL
    SELECT 'BestSellers',      'Best Sellers',      5
) s
WHERE p.`TenantId` = 1 AND p.`Slug` = 'home'
  AND NOT EXISTS (
      SELECT 1 FROM `PageSections` ps
      WHERE ps.`PageId` = p.`PageId` AND ps.`SectionType` = s.`SectionType`
  );

-- Transactional email templates --------------------------------------
INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`) VALUES
    (1, 'order.confirmation', 'Email', 'Your order {{OrderNumber}} is confirmed', 'Hi {{CustomerName}}, thanks for your order {{OrderNumber}}.'),
    (1, 'password.reset',     'Email', 'Reset your password',                     'Use this link to reset your password: {{ResetLink}}');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '014_seed.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '014_seed.sql');
