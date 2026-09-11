-- ---------------------------------------------------------------------------
-- 066_catalogues_page.sql — Catalogues joins the editable-pages family
--
-- Title, published state, search title/description and the intro line were
-- hardcoded in the Angular component. Folded into Pages/PageSections instead
-- (055's pattern, same as About/FAQ/Buying guide/Contact), so admin edits it
-- from Online store > Pages instead of a bespoke screen. The PDF files
-- themselves stay in Catalogues (065) — this only covers the page's own words.
-- ---------------------------------------------------------------------------

INSERT INTO `Pages` (`TenantId`, `Title`, `Slug`, `Type`, `IsPublished`, `MetaTitle`, `MetaDescription`, `CreatedAt`)
SELECT * FROM (
  SELECT 1 AS TenantId, 'Catalogues' AS Title, 'catalogues' AS Slug, 'Custom' AS Type, 1 AS IsPublished,
         'Catalogues — Download design catalogues (PDF)' AS MetaTitle,
         'Download our calendar design catalogues as PDF — browse every design number offline before ordering.' AS MetaDescription,
         NOW() AS CreatedAt
) AS p
WHERE NOT EXISTS (SELECT 1 FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'catalogues');

SET @pid := (SELECT `PageId` FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'catalogues' LIMIT 1);
SET @empty := (SELECT COUNT(*) = 0 FROM `PageSections` WHERE `PageId` = @pid);

INSERT INTO `PageSections` (`PageId`, `SectionType`, `Title`, `Content`, `DisplayOrder`, `IsVisible`, `CreatedAt`)
SELECT * FROM (
  SELECT @pid AS PageId, 'Prose' AS SectionType, 'Catalogues' AS Title,
    '<p>Download our design catalogues as PDF — browse every design number offline, then order by design number on the price list.</p>' AS Content,
    1 AS DisplayOrder, 1 AS IsVisible, NOW() AS CreatedAt
) AS seed
WHERE @empty;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '066_catalogues_page.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '066_catalogues_page.sql');
