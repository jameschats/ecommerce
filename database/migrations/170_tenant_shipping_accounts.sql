-- =====================================================================
-- 170_tenant_shipping_accounts.sql  —  V2 Shiprocket integration SR1.
-- Per-tenant Shiprocket connection (each merchant connects their OWN account;
-- mirrors TenantPaymentAccounts). The API password is stored as ASP.NET
-- Data-Protection ciphertext (never plaintext). Additive (V2 band 170+).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `TenantShippingAccounts` (
    `TenantShippingAccountId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT UNSIGNED NOT NULL,
    `Provider`        VARCHAR(30)   NOT NULL DEFAULT 'Shiprocket',
    `Email`           VARCHAR(200)  NULL,
    `PasswordCipher`  VARCHAR(1000) NULL,           -- data-protected ciphertext
    `PickupPincode`   VARCHAR(10)   NULL,
    `PickupLocation`  VARCHAR(100)  NULL,
    `IsVerified`      TINYINT(1)    NOT NULL DEFAULT 0,
    `IsEnabled`       TINYINT(1)    NOT NULL DEFAULT 0,
    `ConnectedAt`     DATETIME      NULL,
    `CreatedAt`       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`TenantShippingAccountId`),
    UNIQUE KEY `uq_tenantshipping_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '170_tenant_shipping_accounts.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '170_tenant_shipping_accounts.sql');
