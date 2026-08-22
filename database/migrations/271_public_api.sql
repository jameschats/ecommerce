-- =====================================================================
-- 271_public_api.sql — v4 Phase 6, Track A: Public API + Webhooks.
-- ApiKeys authenticate third-party integrators against a deliberately
-- separate, versioned surface (/api/public/v1/) — never the internal
-- admin API the Angular app calls. WebhookSubscriptions/Deliveries
-- give the same platform an event-push channel, with a full delivery
-- audit trail (every attempt persists, same "no black-box" posture as
-- the notification router and the pricing engine's audit log).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `ApiKeys` (
  `ApiKeyId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT UNSIGNED NOT NULL DEFAULT 1,
  `Label` VARCHAR(120) NOT NULL,
  `KeyHash` CHAR(64) NOT NULL,
  `KeyPrefix` VARCHAR(12) NOT NULL,
  `Scopes` VARCHAR(500) NOT NULL,
  `CreatedByUserId` BIGINT UNSIGNED NULL,
  `LastUsedAt` DATETIME NULL,
  `RevokedAt` DATETIME NULL,
  `CreatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`ApiKeyId`),
  UNIQUE KEY `UX_ApiKeys_KeyHash` (`KeyHash`),
  KEY `IX_ApiKeys_Tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `WebhookSubscriptions` (
  `WebhookSubscriptionId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT UNSIGNED NOT NULL DEFAULT 1,
  `Url` VARCHAR(500) NOT NULL,
  `Events` VARCHAR(500) NOT NULL,
  `EncryptedSecret` VARCHAR(500) NOT NULL,
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
  `CreatedByUserId` BIGINT UNSIGNED NULL,
  `CreatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`WebhookSubscriptionId`),
  KEY `IX_WebhookSubscriptions_Tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `WebhookDeliveries` (
  `WebhookDeliveryId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT UNSIGNED NOT NULL DEFAULT 1,
  `WebhookSubscriptionId` BIGINT UNSIGNED NOT NULL,
  `EventType` VARCHAR(60) NOT NULL,
  `Payload` TEXT NOT NULL,
  `Status` VARCHAR(20) NOT NULL DEFAULT 'Pending',
  `AttemptCount` INT NOT NULL DEFAULT 0,
  `LastAttemptAt` DATETIME NULL,
  `LastStatusCode` INT NULL,
  `LastError` VARCHAR(500) NULL,
  `DeliveredAt` DATETIME NULL,
  `CreatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`WebhookDeliveryId`),
  KEY `IX_WebhookDeliveries_Tenant_Subscription` (`TenantId`, `WebhookSubscriptionId`),
  KEY `IX_WebhookDeliveries_Status` (`Status`),
  CONSTRAINT `FK_WebhookDeliveries_Subscriptions` FOREIGN KEY (`WebhookSubscriptionId`) REFERENCES `WebhookSubscriptions` (`WebhookSubscriptionId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '271_public_api.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '271_public_api.sql');
