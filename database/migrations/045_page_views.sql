-- =====================================================================
-- 045_page_views.sql  —  First-party traffic tracking
-- Raw page-view log written by the public POST /api/analytics/track beacon (Angular app,
-- one call per client-side route change, admin routes excluded). VisitorId is a persistent
-- first-party cookie value; SessionId resets per browser tab (sessionStorage). Country/City
-- are resolved server-side at write time via GeoLookupService (no-ops until a GeoLite2
-- database is configured). No rollup table: query-time aggregation is plenty at this
-- traffic scale, matching how SearchLogs/PopularSearches already do it in this schema.
-- Forward-only; idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `PageViews` (
    `PageViewId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`    BIGINT UNSIGNED NOT NULL DEFAULT 1,
    -- VARCHAR, not CHAR(36): MySqlConnector auto-detects fixed CHAR(36) columns as GUID and
    -- hands EF Core a System.Guid where the entity declares string, which throws InvalidCastException.
    `VisitorId`   VARCHAR(36) NOT NULL,
    `SessionId`   VARCHAR(36) NOT NULL,
    `Path`        VARCHAR(500) NOT NULL,
    `Referrer`    VARCHAR(500) NULL,
    `DeviceType`  VARCHAR(20) NOT NULL DEFAULT 'Desktop',
    `Country`     VARCHAR(100) NULL,
    `City`        VARCHAR(100) NULL,
    `CreatedAt`   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`PageViewId`),
    KEY `ix_pageviews_tenant_created` (`TenantId`, `CreatedAt`),
    KEY `ix_pageviews_tenant_session` (`TenantId`, `SessionId`),
    KEY `ix_pageviews_tenant_visitor` (`TenantId`, `VisitorId`, `CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '045_page_views.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '045_page_views.sql');
