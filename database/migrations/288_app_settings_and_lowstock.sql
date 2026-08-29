-- =====================================================================
-- 288_app_settings_and_lowstock.sql  —  App Store: per-installation settings +
-- seed the first-party "Low Stock Alerts" app. Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `AppSettings` (
  `AppSettingId`      BIGINT NOT NULL AUTO_INCREMENT,
  `TenantId`          BIGINT NOT NULL,
  `AppInstallationId` BIGINT NOT NULL,
  `Key`               VARCHAR(60) NOT NULL,
  `Value`             VARCHAR(500) NULL,
  `UpdatedAt`         DATETIME NULL,
  PRIMARY KEY (`AppSettingId`),
  UNIQUE KEY `UQ_AppSetting_Install_Key` (`AppInstallationId`, `Key`),
  KEY `IX_AppSetting_Tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed the first-party Low Stock Alerts app (listed, no client secret needed for one-click first-party install).
INSERT INTO `Apps` (`Name`, `Slug`, `ClientId`, `ClientSecretHash`, `SecretPrefix`, `Description`, `Category`,
                    `RedirectUris`, `RequestedScopes`, `IsEmbedded`, `PricingModel`, `Status`, `IsFirstParty`, `CreatedAt`)
SELECT 'Low Stock Alerts', 'low-stock-alerts', 'wcapp_lowstock', '', '',
       'Get an email when a product runs low, so you never miss a restock.', 'Inventory',
       '', 'inventory:read', 0, 'free', 'listed', 1, UTC_TIMESTAMP()
WHERE NOT EXISTS (SELECT 1 FROM `Apps` WHERE `Slug` = 'low-stock-alerts');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '288_app_settings_and_lowstock.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '288_app_settings_and_lowstock.sql');
