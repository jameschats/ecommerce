-- =====================================================================
-- 167_policies.sql  —  V2 Merchant-Admin M7a: store policies.
-- The fixed set of legal pages (refund / privacy / terms / shipping / contact /
-- legal), edited by the merchant and shown on the storefront + linked in the footer.
-- Body HTML is sanitized on save. Additive (V2 band 160-169).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `StorePolicies` (
    `StorePolicyId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`      BIGINT       NOT NULL DEFAULT 1,
    `Handle`        VARCHAR(40)  NOT NULL,   -- refund | privacy | terms | shipping | contact | legal
    `Title`         VARCHAR(160) NOT NULL,
    `BodyHtml`      MEDIUMTEXT   NULL,
    `CreatedAt`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`     DATETIME     NULL,
    PRIMARY KEY (`StorePolicyId`),
    UNIQUE KEY `uq_storepolicies_tenant_handle` (`TenantId`, `Handle`),
    KEY `ix_storepolicies_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '167_policies.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '167_policies.sql');
