-- =====================================================================
-- 280_articles.sql  —  AI Growth G5: blog / content-marketing articles.
-- Tenant-scoped destination for the AI blog writer + a storefront /blog.
-- Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Articles` (
  `ArticleId`        BIGINT NOT NULL AUTO_INCREMENT,
  `TenantId`         BIGINT NOT NULL,
  `Title`            VARCHAR(255) NOT NULL,
  `Slug`             VARCHAR(255) NOT NULL,
  `Excerpt`          VARCHAR(500) NULL,
  `BodyHtml`         MEDIUMTEXT NOT NULL,
  `CoverImageUrl`    VARCHAR(500) NULL,
  `AuthorName`       VARCHAR(120) NULL,
  `MetaTitle`        VARCHAR(255) NULL,
  `MetaDescription`  VARCHAR(500) NULL,
  `Status`           VARCHAR(20) NOT NULL DEFAULT 'Draft',
  `PublishedAt`      DATETIME NULL,
  `CreatedByUserId`  BIGINT NULL,
  `CreatedAt`        DATETIME NOT NULL,
  `UpdatedAt`        DATETIME NULL,
  PRIMARY KEY (`ArticleId`),
  UNIQUE KEY `UQ_Article_Tenant_Slug` (`TenantId`, `Slug`),
  KEY `IX_Article_Tenant_Status` (`TenantId`, `Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '280_articles.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '280_articles.sql');
