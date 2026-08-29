-- =====================================================================
-- 287_app_marketplace.sql  —  App Store (Shopify-Apps style) S1: app identity + OAuth install.
-- Apps (global) + per-tenant installations + short-lived OAuth codes. Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Apps` (
  `AppId`            BIGINT NOT NULL AUTO_INCREMENT,
  `OwnerUserId`      BIGINT NULL,
  `Name`             VARCHAR(120) NOT NULL,
  `Slug`             VARCHAR(140) NOT NULL,
  `ClientId`         VARCHAR(64) NOT NULL,
  `ClientSecretHash` VARCHAR(128) NOT NULL,
  `SecretPrefix`     VARCHAR(24) NOT NULL DEFAULT '',
  `Description`      VARCHAR(1000) NULL,
  `IconUrl`          VARCHAR(500) NULL,
  `Category`         VARCHAR(60) NULL,
  `RedirectUris`     VARCHAR(1000) NOT NULL DEFAULT '',
  `RequestedScopes`  VARCHAR(500) NOT NULL DEFAULT '',
  `IsEmbedded`       TINYINT(1) NOT NULL DEFAULT 0,
  `EmbedUrl`         VARCHAR(500) NULL,
  `PricingModel`     VARCHAR(20) NOT NULL DEFAULT 'free',
  `Status`           VARCHAR(20) NOT NULL DEFAULT 'draft',
  `IsFirstParty`     TINYINT(1) NOT NULL DEFAULT 0,
  `CreatedAt`        DATETIME NOT NULL,
  `UpdatedAt`        DATETIME NULL,
  PRIMARY KEY (`AppId`),
  UNIQUE KEY `UQ_App_Slug` (`Slug`),
  UNIQUE KEY `UQ_App_ClientId` (`ClientId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AppInstallations` (
  `AppInstallationId` BIGINT NOT NULL AUTO_INCREMENT,
  `TenantId`          BIGINT NOT NULL,
  `AppId`             BIGINT NOT NULL,
  `GrantedScopes`     VARCHAR(500) NOT NULL DEFAULT '',
  `AccessTokenHash`   VARCHAR(128) NOT NULL,
  `TokenPrefix`       VARCHAR(24) NOT NULL DEFAULT '',
  `Status`            VARCHAR(20) NOT NULL DEFAULT 'installed',
  `InstalledByUserId` BIGINT NULL,
  `InstalledAt`       DATETIME NOT NULL,
  `UninstalledAt`     DATETIME NULL,
  `LastUsedAt`        DATETIME NULL,
  PRIMARY KEY (`AppInstallationId`),
  UNIQUE KEY `UQ_AppInstall_Tenant_App` (`TenantId`, `AppId`),
  KEY `IX_AppInstall_TokenHash` (`AccessTokenHash`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AppOAuthCodes` (
  `AppOAuthCodeId` BIGINT NOT NULL AUTO_INCREMENT,
  `CodeHash`       VARCHAR(128) NOT NULL,
  `AppId`          BIGINT NOT NULL,
  `TenantId`       BIGINT NOT NULL,
  `Scopes`         VARCHAR(500) NOT NULL DEFAULT '',
  `UserId`         BIGINT NULL,
  `ExpiresAt`      DATETIME NOT NULL,
  `RedeemedAt`     DATETIME NULL,
  `CreatedAt`      DATETIME NOT NULL,
  PRIMARY KEY (`AppOAuthCodeId`),
  UNIQUE KEY `UQ_AppOAuthCode_Hash` (`CodeHash`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '287_app_marketplace.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '287_app_marketplace.sql');
