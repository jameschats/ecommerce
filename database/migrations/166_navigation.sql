-- =====================================================================
-- 166_navigation.sql  —  V2 Merchant-Admin M6c: Menus + URL redirects.
-- Editable storefront navigation menus (main / footer / account), items stored
-- as JSON (label + url, one level of children). Plus from→to URL redirects.
-- Additive (V2 band 160-169).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Menus` (
    `MenuId`    BIGINT      NOT NULL AUTO_INCREMENT,
    `TenantId`  BIGINT      NOT NULL DEFAULT 1,
    `Handle`    VARCHAR(40) NOT NULL,           -- main-menu | footer | account
    `Title`     VARCHAR(120) NOT NULL,
    `ItemsJson` JSON        NULL,
    `CreatedAt` DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME    NULL,
    PRIMARY KEY (`MenuId`),
    UNIQUE KEY `uq_menus_tenant_handle` (`TenantId`, `Handle`),
    KEY `ix_menus_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `UrlRedirects` (
    `UrlRedirectId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`      BIGINT       NOT NULL DEFAULT 1,
    `FromPath`      VARCHAR(500) NOT NULL,
    `ToPath`        VARCHAR(500) NOT NULL,
    `CreatedAt`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`     DATETIME     NULL,
    PRIMARY KEY (`UrlRedirectId`),
    UNIQUE KEY `uq_urlredirects_tenant_from` (`TenantId`, `FromPath`),
    KEY `ix_urlredirects_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '166_navigation.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '166_navigation.sql');
