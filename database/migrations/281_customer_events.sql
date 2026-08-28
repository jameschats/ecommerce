-- =====================================================================
-- 281_customer_events.sql  —  AI Commerce data layer (Phase 3 Track B).
-- Server-side behavioural events (view/search/add-to-cart/remove/purchase),
-- the shared foundation for Personalization, Trending, and Dynamic Pricing's
-- demand signal. Written in batches off the hot path. Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `CustomerEvents` (
  `CustomerEventId` BIGINT NOT NULL AUTO_INCREMENT,
  `TenantId`        BIGINT NOT NULL,
  `UserId`          BIGINT NULL,
  `SessionId`       VARCHAR(64) NOT NULL,
  `EventType`       VARCHAR(30) NOT NULL,
  `ProductId`       BIGINT NULL,
  `Metadata`        VARCHAR(500) NULL,
  `CreatedAt`       DATETIME NOT NULL,
  PRIMARY KEY (`CustomerEventId`),
  KEY `IX_CustomerEvent_Tenant_Created` (`TenantId`, `CreatedAt`),
  KEY `IX_CustomerEvent_Tenant_Product_Type` (`TenantId`, `ProductId`, `EventType`),
  KEY `IX_CustomerEvent_Tenant_Session` (`TenantId`, `SessionId`),
  KEY `IX_CustomerEvent_Tenant_User` (`TenantId`, `UserId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '281_customer_events.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '281_customer_events.sql');
