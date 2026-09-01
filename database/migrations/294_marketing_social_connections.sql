-- =====================================================================
-- 294_marketing_social_connections.sql — Marketing Studio MS1: per-tenant
-- social OAuth connections (one row per connected platform; tokens stored
-- encrypted by the app via IDataProtection). Standard SaaS model: one
-- platform app per network, each merchant connects their own account.
-- Marketing* cluster, no FKs into core commerce tables (extractable).
-- Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `SocialConnections` (
    `SocialConnectionId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`           BIGINT       NOT NULL,
    `Platform`           VARCHAR(20)  NOT NULL,
    `AccessTokenCipher`  TEXT         NULL,
    `RefreshTokenCipher` TEXT         NULL,
    `ExternalAccountId`  VARCHAR(190) NULL,
    `AccountName`        VARCHAR(200) NULL,
    `Scopes`             VARCHAR(500) NULL,
    `ExpiresAt`          DATETIME(6)  NULL,
    `ConnectedAt`        DATETIME(6)  NOT NULL,
    `UpdatedAt`          DATETIME(6)  NULL,
    PRIMARY KEY (`SocialConnectionId`),
    UNIQUE KEY `UX_SocialConnections_Tenant_Platform` (`TenantId`, `Platform`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '294_marketing_social_connections.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '294_marketing_social_connections.sql');
