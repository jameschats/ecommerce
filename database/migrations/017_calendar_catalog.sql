-- =====================================================================
-- 017_calendar_catalog.sql  —  Demo data: turn the store into "CalendarShop"
-- DATA ONLY. Schema stays generic — calendar specifics live in the generic
-- Attributes (Material/Orientation/Pages) and Variant Options (Size/Finish).
-- =====================================================================

-- Rebrand
UPDATE `Settings` SET `SettingValue` = 'CalendarShop', `UpdatedAt` = CURRENT_TIMESTAMP
WHERE `TenantId` = 1 AND `SettingKey` = 'SiteName';

-- Retire the old demo catalog (Laptops/Batteries/Dell)
UPDATE `Products`   SET `IsDeleted` = 1, `IsActive` = 0 WHERE `TenantId` = 1;
UPDATE `Categories` SET `IsActive`  = 0 WHERE `TenantId` = 1;

-- Categories ----------------------------------------------------------
INSERT INTO `Categories` (`TenantId`, `Name`, `Slug`, `Description`, `ImageUrl`, `DisplayOrder`, `IsActive`, `CreatedAt`) VALUES
 (1, 'Wall Calendars',     'wall-calendars',     'Premium customizable 12-month wall calendars.', 'https://picsum.photos/seed/wallcal/500/500',   1, 1, CURRENT_TIMESTAMP),
 (1, 'Desk Calendars',     'desk-calendars',     'Smart desk calendars for home and office.',     'https://picsum.photos/seed/deskcal/500/500',   2, 1, CURRENT_TIMESTAMP),
 (1, 'Tent Calendars',     'tent-calendars',     'Standing tent calendars for your desk.',        'https://picsum.photos/seed/tentcal/500/500',   3, 1, CURRENT_TIMESTAMP),
 (1, 'Pocket Calendars',   'pocket-calendars',   'Handy pocket-sized calendars.',                 'https://picsum.photos/seed/pocketcal/500/500', 4, 1, CURRENT_TIMESTAMP),
 (1, 'Magnet Calendars',   'magnet-calendars',   'Fridge-magnet calendars.',                      'https://picsum.photos/seed/magnetcal/500/500', 5, 1, CURRENT_TIMESTAMP),
 (1, 'Mouse Pad Calendars','mouse-pad-calendars','Mouse pads with a built-in calendar.',          'https://picsum.photos/seed/mousepadcal/500/500',6,1, CURRENT_TIMESTAMP);

-- Products ------------------------------------------------------------
INSERT INTO `Products`
 (`TenantId`, `CategoryId`, `Sku`, `Name`, `Slug`, `ShortDescription`, `Description`, `HsnCode`, `Price`, `CompareAtPrice`, `Status`, `IsFeatured`, `IsActive`, `CreatedAt`)
VALUES
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='wall-calendars'),     'WALL-2026',      'Wall Calendar 2026',            'wall-calendar-2026',            'Premium 12-month wall calendar, fully customizable with your photos and logo.', 'Full-colour HD printing on premium 300 GSM art paper. Comes with a sturdy hanging loop. Customizable with image, text, company name and logo.', '4910', 660.00, 899.00, 'Active', 1, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='wall-calendars'),     'WALL-4SHEET',    'Four Sheeter Wall Calendar',    'four-sheeter-wall-calendar',    'Elegant four-sheet wall calendar with large photo area.', 'Four-sheet design, three months per sheet. Premium paper, vivid printing.', '4910', 780.00, 999.00, 'Active', 1, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='wall-calendars'),     'WALL-PREMIUM',   'Premium Themed Wall Calendar',  'premium-themed-wall-calendar',  'Designer themed wall calendar.', 'Curated artwork themes printed on heavy art paper.', '4910', 1200.00, NULL, 'Active', 0, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='desk-calendars'),     'DESK-2026',      'Desk Calendar 2026',            'desk-calendar-2026',            'Compact desk calendar for your workspace.', 'Spiral-bound desk calendar with monthly sheets and notes area.', '4910', 470.00, 600.00, 'Active', 1, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='desk-calendars'),     'DESK-PHOTO',     'Desk Calendar with Photo Frame','desk-calendar-with-photo-frame','Desk calendar that doubles as a photo frame.', 'Combine a calendar with a personalised photo frame.', '4910', 525.00, NULL, 'Active', 0, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='desk-calendars'),     'DESK-PERPETUAL', 'Perpetual Desk Calendar',       'perpetual-desk-calendar',       'A perpetual calendar that never expires.', 'Flip-block perpetual desk calendar in premium finish.', '4910', 525.00, NULL, 'Active', 0, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='tent-calendars'),     'TENT-2026',      'Tent Calendar 2026',            'tent-calendar-2026',            'Standing tent calendar for desks.', 'Self-standing tent calendar, ideal for desks and counters.', '4910', 190.00, 250.00, 'Active', 0, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='pocket-calendars'),   'POCKET-2026',    'Pocket Calendar 2026',          'pocket-calendar-2026',          'Handy pocket-sized yearly calendar.', 'Credit-card-friendly pocket calendar, sold in packs.', '4910', 190.00, NULL, 'Active', 1, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='magnet-calendars'),   'MAGNET-2026',    'Magnet Calendar 2026',          'magnet-calendar-2026',          'Fridge-magnet calendar.', 'Flexible magnet calendar that sticks to any metal surface.', '4910', 250.00, NULL, 'Active', 0, 1, CURRENT_TIMESTAMP),
 (1, (SELECT CategoryId FROM Categories WHERE TenantId=1 AND Slug='mouse-pad-calendars'),'MOUSEPAD-2026',  'Mouse Pad Calendar 2026',       'mouse-pad-calendar-2026',       'Mouse pad with a built-in yearly calendar.', 'Smooth-surface mouse pad printed with a full-year calendar.', '4910', 350.00, 450.00, 'Active', 1, 1, CURRENT_TIMESTAMP);

-- Product images (primary + a couple of gallery shots each) ------------
INSERT INTO `ProductImages` (`ProductId`, `Url`, `AltText`, `DisplayOrder`, `IsPrimary`, `CreatedAt`)
SELECT p.ProductId, CONCAT('https://picsum.photos/seed/', p.Sku, '-a/800/800'), p.Name, 0, 1, CURRENT_TIMESTAMP FROM Products p WHERE p.TenantId=1 AND p.IsDeleted=0 AND p.HsnCode='4910';

INSERT INTO `ProductImages` (`ProductId`, `Url`, `AltText`, `DisplayOrder`, `IsPrimary`, `CreatedAt`)
SELECT p.ProductId, CONCAT('https://picsum.photos/seed/', p.Sku, '-b/800/800'), p.Name, 1, 0, CURRENT_TIMESTAMP FROM Products p WHERE p.TenantId=1 AND p.IsDeleted=0 AND p.HsnCode='4910';
INSERT INTO `ProductImages` (`ProductId`, `Url`, `AltText`, `DisplayOrder`, `IsPrimary`, `CreatedAt`)
SELECT p.ProductId, CONCAT('https://picsum.photos/seed/', p.Sku, '-c/800/800'), p.Name, 2, 0, CURRENT_TIMESTAMP FROM Products p WHERE p.TenantId=1 AND p.IsDeleted=0 AND p.HsnCode='4910';

-- Generic attributes (specs) — NOT calendar-specific columns ----------
INSERT INTO `Attributes` (`TenantId`, `Name`, `Code`, `DataType`, `IsFilterable`, `IsActive`, `CreatedAt`) VALUES
 (1, 'Material',    'material',    'string', 0, 1, CURRENT_TIMESTAMP),
 (1, 'Orientation', 'orientation', 'string', 1, 1, CURRENT_TIMESTAMP),
 (1, 'Pages',       'pages',       'string', 0, 1, CURRENT_TIMESTAMP);

-- Assign specs to wall calendars via the generic ProductAttributeValues
INSERT INTO `ProductAttributeValues` (`ProductId`, `AttributeId`, `ValueText`, `CreatedAt`)
SELECT p.ProductId, a.AttributeId, v.val, CURRENT_TIMESTAMP
FROM Products p
JOIN Attributes a ON a.TenantId = 1
JOIN (
  SELECT 'material' AS code, '300 GSM premium art paper' AS val UNION ALL
  SELECT 'orientation', 'Portrait' UNION ALL
  SELECT 'pages', '12 + cover'
) v ON v.code = a.Code
WHERE p.TenantId = 1 AND p.Sku IN ('WALL-2026','WALL-4SHEET');

-- Generic variants for the flagship (Size + Finish as variant options) -
INSERT INTO `ProductVariants` (`ProductId`, `Sku`, `Name`, `PriceAdjustment`, `IsActive`, `CreatedAt`)
SELECT p.ProductId, CONCAT('WALL-2026-', x.suffix), x.label, x.adj, 1, CURRENT_TIMESTAMP
FROM Products p
JOIN (
  SELECT 'A4G' AS suffix, 'A4 / Glossy' AS label, 0.00 AS adj UNION ALL
  SELECT 'A3G', 'A3 / Glossy', 200.00 UNION ALL
  SELECT 'A4M', 'A4 / Matte', 0.00
) x
WHERE p.TenantId = 1 AND p.Sku = 'WALL-2026';

INSERT INTO `VariantOptions` (`ProductVariantId`, `OptionName`, `OptionValue`, `CreatedAt`)
SELECT v.ProductVariantId, o.name, o.val, CURRENT_TIMESTAMP
FROM ProductVariants v
JOIN (
  SELECT 'WALL-2026-A4G' AS sku, 'Size' AS name, 'A4 (210 x 297 mm)' AS val UNION ALL
  SELECT 'WALL-2026-A4G', 'Finish', 'Glossy' UNION ALL
  SELECT 'WALL-2026-A3G', 'Size', 'A3 (420 x 297 mm)' UNION ALL
  SELECT 'WALL-2026-A3G', 'Finish', 'Glossy' UNION ALL
  SELECT 'WALL-2026-A4M', 'Size', 'A4 (210 x 297 mm)' UNION ALL
  SELECT 'WALL-2026-A4M', 'Finish', 'Matte'
) o ON o.sku = v.Sku;

-- Testimonials home section (generic CMS section type) ----------------
INSERT INTO `PageSections` (`PageId`, `SectionType`, `Title`, `DisplayOrder`, `IsVisible`, `CreatedAt`)
SELECT p.PageId, 'Testimonials', 'True Journeys. True Transformation.', 6, 1, CURRENT_TIMESTAMP
FROM Pages p
WHERE p.TenantId = 1 AND p.Slug = 'home'
  AND NOT EXISTS (SELECT 1 FROM PageSections ps WHERE ps.PageId = p.PageId AND ps.SectionType = 'Testimonials');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '017_calendar_catalog.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '017_calendar_catalog.sql');
