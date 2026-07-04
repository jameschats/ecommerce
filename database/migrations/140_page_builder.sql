-- =====================================================================
-- 140_page_builder.sql  —  V2-6: evolve CMS-lite into a sections-and-blocks
-- storefront builder. Adds per-section settings + nested blocks (JSON) and a
-- TenantId on PageSections (was only reachable via Page). Additive (V2 140-169).
-- =====================================================================

ALTER TABLE `PageSections`
    ADD COLUMN `TenantId` BIGINT NULL AFTER `PageSectionId`,
    ADD COLUMN `Settings` JSON   NULL AFTER `Title`,   -- per-section settings (heading, columns, colors, ...)
    ADD COLUMN `Blocks`   JSON   NULL AFTER `Settings`; -- ordered child blocks (hero slides, testimonial items)

-- Backfill TenantId from the parent Page, then enforce NOT NULL (auto-stamped on insert).
UPDATE `PageSections` ps JOIN `Pages` p ON ps.`PageId` = p.`PageId`
   SET ps.`TenantId` = p.`TenantId`
 WHERE ps.`TenantId` IS NULL;
UPDATE `PageSections` SET `TenantId` = 1 WHERE `TenantId` IS NULL;

ALTER TABLE `PageSections` MODIFY COLUMN `TenantId` BIGINT NOT NULL DEFAULT 1;
CREATE INDEX `ix_pagesections_tenant` ON `PageSections` (`TenantId`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '140_page_builder.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '140_page_builder.sql');
