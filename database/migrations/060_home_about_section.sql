-- ---------------------------------------------------------------------------
-- 060_home_about_section.sql — an independent "Who we are?" section for the home page
--
-- The home page write-up used to be the About Us page's own sections, reused
-- verbatim so there was only one copy to maintain. The shop now wants the two
-- to be able to diverge — the home version may be shorter or worded
-- differently from the About Us page — so this seeds a new, separate content
-- page (slug `home-about`) with a copy of About's current Prose/Stats/Cards
-- sections (not the Cta — the price list right below already serves that
-- purpose on the home page). From here the two are independently editable
-- from the same Pages admin screen; editing one never touches the other.
-- ---------------------------------------------------------------------------

INSERT INTO `Pages` (`TenantId`, `Title`, `Slug`, `Type`, `IsPublished`, `MetaTitle`, `MetaDescription`, `CreatedAt`)
SELECT 1, 'Who we are?', 'home-about', 'Custom', 1, NULL, NULL, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'home-about');

INSERT INTO `PageSections` (`PageId`, `SectionType`, `Title`, `Content`, `DisplayOrder`, `IsVisible`, `CreatedAt`)
SELECT
  (SELECT PageId FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'home-about'),
  src.`SectionType`, src.`Title`, src.`Content`, src.`DisplayOrder`, src.`IsVisible`, NOW()
FROM `PageSections` src
WHERE src.`PageId` = (SELECT PageId FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'about')
  AND src.`SectionType` <> 'Cta'
  AND NOT EXISTS (
    SELECT 1 FROM `PageSections` dst
    WHERE dst.`PageId` = (SELECT PageId FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'home-about')
  );

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '060_home_about_section.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '060_home_about_section.sql');
