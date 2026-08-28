-- =====================================================================
-- 274_back_in_stock_requests.sql  —  "Email me when back in stock" opt-ins.
-- One row per shopper request; NotifiedAt is set when the product restocks and
-- the email goes out. Tenant-scoped. Additive.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `BackInStockRequests` (
    `BackInStockRequestId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`   BIGINT UNSIGNED NOT NULL,
    `ProductId`  BIGINT UNSIGNED NOT NULL,
    `Email`      VARCHAR(256) NOT NULL,
    `UserId`     BIGINT UNSIGNED NULL,
    `CreatedAt`  DATETIME NOT NULL,
    `NotifiedAt` DATETIME NULL,
    PRIMARY KEY (`BackInStockRequestId`),
    KEY `ix_bisr_pending` (`TenantId`, `ProductId`, `NotifiedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '274_back_in_stock_requests.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '274_back_in_stock_requests.sql');
