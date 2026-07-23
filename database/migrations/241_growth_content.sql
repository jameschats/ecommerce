-- =====================================================================
-- 241_growth_content.sql  —  The AI Growth content library (G1).
--
-- Every generation is saved here so a merchant can find, re-edit and reuse
-- it — content they can't retrieve is content they'll regenerate and pay for
-- twice. ProductId is optional (a festival post may reference no single
-- product). Status tracks the generate → review → use flow; nothing here is
-- ever auto-published.
--
-- Band 240-249 is the AI-Growth module (documents/ai-growth/README.md).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `GrowthContents` (
    `GrowthContentId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT       NOT NULL DEFAULT 1,
    `ContentType`     VARCHAR(30)  NOT NULL,               -- instagram-caption | facebook-post | whatsapp | email | product-description | seo | festival-offer
    `ProductId`       BIGINT       NULL,
    `Language`        VARCHAR(20)  NOT NULL DEFAULT 'English',
    `Title`           VARCHAR(200) NULL,                   -- e.g. the email subject; null for a plain caption
    `Body`            TEXT         NOT NULL,
    `Status`          VARCHAR(20)  NOT NULL DEFAULT 'Draft',   -- Draft | Kept | Discarded
    `CreatedByUserId` BIGINT       NULL,
    `CreatedAt`       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME     NULL,
    PRIMARY KEY (`GrowthContentId`),
    KEY `IX_GrowthContents_Tenant_Type` (`TenantId`, `ContentType`, `GrowthContentId`),
    KEY `IX_GrowthContents_Product` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '241_growth_content.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '241_growth_content.sql');
