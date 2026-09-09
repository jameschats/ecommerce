-- =====================================================================
-- 063_widen_category_description.sql — Categories.Description was
-- VARCHAR(500). Editing a category's description with more than 500
-- characters hit MySQL's "Data too long for column" on save, which the
-- generic exception handler surfaces as an unhelpful "An unexpected
-- error occurred." with no indication of the real cause. Widened to
-- TEXT, matching Products.Description (003_catalog.sql), which already
-- needed the same room for the same reason.
-- Forward-only.
-- =====================================================================

ALTER TABLE `Categories`
    MODIFY COLUMN `Description` TEXT NULL;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '063_widen_category_description.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '063_widen_category_description.sql');
