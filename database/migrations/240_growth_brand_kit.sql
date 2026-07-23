-- =====================================================================
-- 240_growth_brand_kit.sql  —  Per-tenant brand voice for AI Growth (G1).
--
-- Feeds every marketing-content prompt so output sounds like the store, not
-- like a generic assistant: tone, the language to write in, who the customer
-- is, whether to use emoji, a hashtag set to reuse, and a do-not-say list the
-- prompt must respect. One row per tenant; created lazily on first use.
--
-- Band 240-249 is the AI-Growth module (documents/ai-growth/README.md).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `GrowthBrandKits` (
    `GrowthBrandKitId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT       NOT NULL DEFAULT 1,
    `Tone`             VARCHAR(20)  NOT NULL DEFAULT 'friendly',   -- friendly | premium | value | playful
    `Language`         VARCHAR(20)  NOT NULL DEFAULT 'English',    -- English | Hindi | Tamil | Telugu | Hinglish
    `Audience`         VARCHAR(300) NULL,                          -- "young families in Tamil Nadu", free text
    `UseEmoji`         TINYINT(1)   NOT NULL DEFAULT 1,
    `Hashtags`         VARCHAR(500) NULL,                          -- space/# separated, reused across posts
    `DoNotSay`         VARCHAR(500) NULL,                          -- comma-separated words/claims to avoid
    `CreatedAt`        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME     NULL,
    PRIMARY KEY (`GrowthBrandKitId`),
    UNIQUE KEY `UX_GrowthBrandKits_Tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '240_growth_brand_kit.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '240_growth_brand_kit.sql');
