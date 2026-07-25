-- ---------------------------------------------------------------------------
-- 035_product_design_no.sql — Design No as its own field
--
-- Phase 1 originally mapped the price list's "Design No" column onto Products.Sku
-- (design.md §5.1). That was wrong: SKU is the internal stock code, while the design
-- number is the trade-facing identifier the calendar business actually quotes, and the
-- two are not the same value.
--
-- Nullable on purpose. Existing products have no design number, and the price list falls
-- back to SKU when it is blank, so nothing breaks before the catalogue is imported.
-- ---------------------------------------------------------------------------

SET @s := DATABASE();

SET @c := (SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema=@s AND table_name='Products' AND column_name='DesignNo');
SET @q := IF(@c=0, 'ALTER TABLE `Products` ADD COLUMN `DesignNo` VARCHAR(60) NULL AFTER `Sku`', 'SELECT 1');
PREPARE st FROM @q; EXECUTE st; DEALLOCATE PREPARE st;

-- Indexed, not unique: the catalogue is mid-migration and a unique constraint would reject
-- a spreadsheet with one accidental duplicate rather than reporting it row by row.
SET @i := (SELECT COUNT(*) FROM information_schema.statistics
            WHERE table_schema=@s AND table_name='Products' AND index_name='IX_Products_DesignNo');
SET @q := IF(@i=0, 'CREATE INDEX `IX_Products_DesignNo` ON `Products` (`TenantId`, `DesignNo`)', 'SELECT 1');
PREPARE st FROM @q; EXECUTE st; DEALLOCATE PREPARE st;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '035_product_design_no.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '035_product_design_no.sql');
