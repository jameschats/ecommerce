-- =====================================================================
-- 293_marketing_studio_brand.sql — Marketing Studio MS0: the per-tenant
-- VISUAL brand kit (logo, company name, brand colours, fonts, social
-- handles, include/exclude defaults). Complements GrowthBrandKits (the
-- brand VOICE). Also turns on the `marketing_studio` plan feature for all
-- plans (flagship pillar — included in every tier), using the same
-- Plan.Features mechanism as migrations 242 (growth) / 270 (dynamic-pricing).
-- Idempotent. Tables use the Marketing* cluster prefix, no FKs into core
-- commerce tables (soft id refs only) so the module can be extracted later.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `MarketingBrandProfiles` (
    `MarketingBrandProfileId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`               BIGINT       NOT NULL,
    `CompanyName`            VARCHAR(200) NULL,
    `Tagline`                VARCHAR(300) NULL,
    `LogoUrl`                VARCHAR(500) NULL,
    `PrimaryColor`           VARCHAR(9)   NOT NULL DEFAULT '#111827',
    `SecondaryColor`         VARCHAR(9)   NOT NULL DEFAULT '#6b7280',
    `AccentColor`            VARCHAR(9)   NOT NULL DEFAULT '#2563eb',
    `Font`                   VARCHAR(80)  NULL,
    `IncludeLogoByDefault`   TINYINT(1)   NOT NULL DEFAULT 1,
    `IncludeNameByDefault`   TINYINT(1)   NOT NULL DEFAULT 1,
    `InstagramHandle`        VARCHAR(120) NULL,
    `FacebookHandle`         VARCHAR(120) NULL,
    `LinkedInHandle`         VARCHAR(120) NULL,
    `PinterestHandle`        VARCHAR(120) NULL,
    `YouTubeHandle`          VARCHAR(120) NULL,
    `WhatsAppNumber`         VARCHAR(30)  NULL,
    `WebsiteUrl`             VARCHAR(300) NULL,
    `CreatedAt`              DATETIME(6)  NOT NULL,
    `UpdatedAt`              DATETIME(6)  NULL,
    PRIMARY KEY (`MarketingBrandProfileId`),
    UNIQUE KEY `UX_MarketingBrandProfiles_Tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Turn on the marketing_studio feature for every plan (flagship pillar).
UPDATE `Plans`
   SET `Features` = JSON_ARRAY_APPEND(`Features`, '$', 'marketing_studio')
 WHERE `Features` IS NOT NULL AND `Features` != ''
   AND NOT JSON_CONTAINS(`Features`, '"marketing_studio"', '$');

UPDATE `Plans`
   SET `Features` = '["marketing_studio"]'
 WHERE `Features` IS NULL OR `Features` = '';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '293_marketing_studio_brand.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '293_marketing_studio_brand.sql');
