-- =====================================================================
-- 150_customer_profiles.sql  —  V2 Merchant-Admin M2: Customers.
-- Per-customer marketing consent + admin notes + tags, keyed to a User.
-- The customer identity itself stays on `Users` (CUSTOMER role); this table
-- only holds the merchant-managed CRM fields. Additive (V2 band 150-159).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `CustomerProfiles` (
    `CustomerProfileId`        BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`                 BIGINT       NOT NULL DEFAULT 1,
    `UserId`                   BIGINT       NOT NULL,
    `AcceptsEmailMarketing`    TINYINT(1)   NOT NULL DEFAULT 0,
    `AcceptsSmsMarketing`      TINYINT(1)   NOT NULL DEFAULT 0,
    `AcceptsWhatsappMarketing` TINYINT(1)   NOT NULL DEFAULT 0,
    `Notes`                    VARCHAR(2000) NULL,
    `Tags`                     VARCHAR(1000) NULL,          -- comma-separated
    `CreatedAt`                DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`                DATETIME     NULL,
    PRIMARY KEY (`CustomerProfileId`),
    UNIQUE KEY `uq_customerprofiles_user` (`TenantId`, `UserId`),
    KEY `ix_customerprofiles_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '150_customer_profiles.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '150_customer_profiles.sql');
