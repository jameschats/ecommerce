-- =============================================================================
-- Apparel demo catalog + theme copy for the "acme" store (WavCommerce)
-- -----------------------------------------------------------------------------
-- Turns the acme storefront into a populated apparel shop: 5 categories,
-- 20 products (images / prices / discounts / stock / featured flags), and an
-- apparel-worded home hero + USP row.
--
-- • Tenant is resolved by SLUG (no hard-coded ids) → run-anywhere.
-- • Idempotent: re-running replaces the demo rows (matched by the 'APP-%' SKU
--   prefix and the fixed category slugs) — it never touches real products.
-- • Images use placehold.co (always-loading, on-brand placeholders). Swapping
--   in real licensed apparel photos later is a one-column update on the Url.
--
-- Run:  mysql -u root -p<pwd> ecommerce < database/seeds/acme_apparel_demo.sql
-- =============================================================================

-- ---- 0. Resolve the tenant (STOP if this prints NULL) -----------------------
SET @slug := 'acme';
SET @tid  := (SELECT TenantId FROM Tenants WHERE Slug = @slug LIMIT 1);
SELECT @tid AS resolved_tenant_id, @slug AS slug;   -- must show a real id

-- ---- 1. Categories (upsert by unique TenantId+Slug) -------------------------
INSERT INTO Categories (TenantId, Name, Slug, Description, ImageUrl, DisplayOrder, IsActive) VALUES
  (@tid, 'Women',       'women',       'Dresses, tops, denim & ethnic wear', 'https://placehold.co/600x450/be185d/ffffff?text=Women',       1, 1),
  (@tid, 'Men',         'men',         'Shirts, tees, trousers & jackets',   'https://placehold.co/600x450/1e293b/ffffff?text=Men',         2, 1),
  (@tid, 'Footwear',    'footwear',    'Sneakers, loafers, heels & more',    'https://placehold.co/600x450/78350f/ffffff?text=Footwear',    3, 1),
  (@tid, 'Accessories', 'accessories', 'Bags, watches, belts & eyewear',     'https://placehold.co/600x450/ca8a04/1e293b?text=Accessories', 4, 1),
  (@tid, 'Kids',        'kids',        'Everyday styles for little ones',    'https://placehold.co/600x450/0ea5e9/ffffff?text=Kids',        5, 1)
ON DUPLICATE KEY UPDATE
  Name = VALUES(Name), Description = VALUES(Description), ImageUrl = VALUES(ImageUrl),
  DisplayOrder = VALUES(DisplayOrder), IsActive = 1;

-- ---- 2. Clean any previous demo rows (safe: only the 'APP-%' demo SKUs) ------
DELETE i  FROM Inventory i     JOIN Products p ON p.ProductId = i.ProductId  WHERE p.TenantId = @tid AND p.Sku LIKE 'APP-%';
DELETE pi FROM ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId WHERE p.TenantId = @tid AND p.Sku LIKE 'APP-%';
DELETE     FROM Products                                                      WHERE TenantId = @tid AND Sku LIKE 'APP-%';

-- ---- 3. Products (bulk insert; category matched by slug) ---------------------
INSERT INTO Products
  (TenantId, CategoryId, Sku, Name, Slug, ShortDescription, Description, Price, CompareAtPrice, CostPrice, HsnCode, Status, IsFeatured, IsActive)
SELECT @tid, c.CategoryId, d.Sku, d.Name, d.Slug, d.ShortDesc, d.Descr, d.Price, d.Compare, d.Cost, '6109', 'Active', d.Featured, 1
FROM (
  --      Cat            Sku          Name                       Slug                         ShortDesc                                  Price  Compare Cost  Feat
  SELECT 'women'       AS Cat, 'APP-W-001' AS Sku, 'Floral Wrap Dress'        AS Name, 'floral-wrap-dress'        AS Slug, 'Breezy viscose wrap dress with a flattering tie waist.'   AS ShortDesc, 'A breezy viscose wrap dress with a flattering tie waist and a soft floral print — an easy pick for warm days and evenings out.' AS Descr, 1299 AS Price, 1999 AS Compare, 700  AS Cost, 1 AS Featured
  UNION ALL SELECT 'women',       'APP-W-002', 'Ribbed Knit Top',          'ribbed-knit-top',          'Soft stretch-ribbed top that layers with everything.',        'Soft stretch-ribbed knit top with a fitted silhouette — layers effortlessly under jackets or stands alone.',                 699,  999,  350, 0
  UNION ALL SELECT 'women',       'APP-W-003', 'High-Waist Denim Jeans',   'high-waist-denim-jeans',   'Sculpting high-rise jeans in premium stretch denim.',         'High-rise, sculpting jeans in premium stretch denim with a clean straight leg — comfortable enough for all day.',           1499, 2199, 800, 1
  UNION ALL SELECT 'women',       'APP-W-004', 'Cotton A-Line Kurti',      'cotton-a-line-kurti',      'Handblock-inspired cotton kurti for everyday ease.',          'Lightweight cotton A-line kurti with a handblock-inspired print — breathable, comfortable and easy to style.',              899,  1299, 450, 0
  UNION ALL SELECT 'men',         'APP-M-001', 'Oxford Cotton Shirt',      'oxford-cotton-shirt',      'Crisp button-down in breathable Oxford cotton.',              'A crisp, versatile button-down in breathable Oxford cotton — dress it up for work or roll the sleeves for the weekend.',    1199, 1799, 600, 1
  UNION ALL SELECT 'men',         'APP-M-002', 'Classic Crew Tee',         'classic-crew-tee',         'Everyday combed-cotton tee with a clean fit.',                'Everyday essential tee in soft combed cotton with a clean, modern fit that keeps its shape wash after wash.',                499,  799,  220, 0
  UNION ALL SELECT 'men',         'APP-M-003', 'Slim-Fit Chinos',          'slim-fit-chinos',          'Stretch-cotton chinos that move with you.',                   'Slim-fit chinos cut from stretch cotton twill — smart enough for the office, comfortable enough for travel.',              1399, 1999, 750, 0
  UNION ALL SELECT 'men',         'APP-M-004', 'Bomber Jacket',            'bomber-jacket',            'Lightweight bomber with ribbed trims.',                       'A lightweight bomber jacket with ribbed collar and cuffs and a water-repellent shell — an easy layer for cooler evenings.',  2499, 3499, 1400, 1
  UNION ALL SELECT 'footwear',    'APP-F-001', 'Canvas Sneakers',          'canvas-sneakers',          'Low-top canvas sneakers with a cushioned sole.',              'Low-top canvas sneakers with a cushioned insole and vulcanised rubber sole — a wardrobe staple in every season.',           1799, 2499, 900, 1
  UNION ALL SELECT 'footwear',    'APP-F-002', 'Leather Loafers',          'leather-loafers',          'Hand-finished leather loafers for smart days.',               'Hand-finished genuine-leather loafers with a comfortable padded footbed — polished enough for work, easy all day.',          2299, 2999, 1200, 0
  UNION ALL SELECT 'footwear',    'APP-F-003', 'Everyday Running Shoes',   'everyday-running-shoes',   'Breathable knit running shoes with responsive foam.',         'Breathable engineered-knit running shoes with a responsive foam midsole and grippy outsole for daily miles.',               2599, 3499, 1300, 0
  UNION ALL SELECT 'footwear',    'APP-F-004', 'Block Heel Sandals',       'block-heel-sandals',       'Comfortable block heels that go day to night.',               'Elegant block-heel sandals with cushioned straps — a comfortable heel that carries you from day to night.',                 1599, 2199, 800, 0
  UNION ALL SELECT 'accessories', 'APP-A-001', 'Leather Tote Bag',         'leather-tote-bag',         'Roomy pebbled-leather tote with a laptop sleeve.',            'A roomy pebbled-leather tote with an internal laptop sleeve and zip pocket — structured, timeless and hard-wearing.',        1899, 2699, 950, 1
  UNION ALL SELECT 'accessories', 'APP-A-002', 'Aviator Sunglasses',       'aviator-sunglasses',       'Polarised aviators with UV400 protection.',                   'Classic polarised aviators with UV400 protection and a lightweight metal frame — glare-free and easy to wear.',             899,  1499, 300, 0
  UNION ALL SELECT 'accessories', 'APP-A-003', 'Minimalist Watch',         'minimalist-watch',         'Slim quartz watch with a mesh strap.',                        'A slim minimalist quartz watch with a brushed case and interchangeable mesh strap — understated and versatile.',            2999, 3999, 1500, 0
  UNION ALL SELECT 'accessories', 'APP-A-004', 'Woven Leather Belt',       'woven-leather-belt',       'Full-grain woven belt with a brushed buckle.',                'A full-grain woven leather belt with a brushed metal buckle — flexible sizing and a smart, textured finish.',               699,  999,  300, 0
  UNION ALL SELECT 'kids',        'APP-K-001', 'Kids Graphic Tee',         'kids-graphic-tee',         'Soft cotton tee with a playful print.',                       'A soft 100% cotton kids tee with a playful print and a tagless neck for itch-free all-day comfort.',                         399,  599,  180, 0
  UNION ALL SELECT 'kids',        'APP-K-002', 'Kids Denim Dungaree',      'kids-denim-dungaree',      'Adjustable denim dungarees built for play.',                  'Adjustable, hard-wearing denim dungarees with roomy pockets — built for climbing, painting and everything in between.',      1099, 1599, 550, 1
  UNION ALL SELECT 'kids',        'APP-K-003', 'Kids Zip Hoodie',          'kids-zip-hoodie',          'Cosy fleece-lined hoodie with a full zip.',                   'A cosy fleece-lined full-zip hoodie with a snug hood and cuffs — the go-to layer for cooler mornings.',                      899,  1299, 450, 0
  UNION ALL SELECT 'kids',        'APP-K-004', 'Kids Jogger Pants',        'kids-jogger-pants',        'Stretch joggers with an easy elastic waist.',                 'Stretch cotton-blend joggers with an easy elastic waist and cuffed ankles — comfy for school and play alike.',              699,  999,  350, 0
) d
JOIN Categories c ON c.TenantId = @tid AND c.Slug = d.Cat;

-- ---- 4. Primary image per product (generated from the name) ------------------
INSERT INTO ProductImages (ProductId, Url, AltText, IsPrimary, DisplayOrder)
SELECT p.ProductId,
       CONCAT('https://placehold.co/600x750/ede9e3/44403c?text=', REPLACE(p.Name, ' ', '+')),
       p.Name, 1, 0
FROM Products p
WHERE p.TenantId = @tid AND p.Sku LIKE 'APP-%';

-- ---- 5. Stock (in-stock so Add-to-cart works) -------------------------------
INSERT INTO Inventory (TenantId, ProductId, AvailableQty, ReservedQty, ReorderLevel)
SELECT @tid, p.ProductId, 50, 0, 5
FROM Products p
WHERE p.TenantId = @tid AND p.Sku LIKE 'APP-%';

-- ---- 6. Apparel home copy: rebrand the hero + USP row -----------------------
-- Scoped to THIS tenant's home (index) template only. No-op if the theme has
-- no Hero/Multicolumn section (safe). Category/product rail headings already
-- read fine for apparel, so they are left as-is.
UPDATE ThemeSections ts
  JOIN ThemeTemplates tt ON tt.ThemeTemplateId = ts.ThemeTemplateId
  JOIN Themes th         ON th.ThemeId = tt.ThemeId
SET ts.Blocks = '[{"image":"https://placehold.co/1600x600/1e293b/f8fafc?text=New+Season+Arrivals","heading":"Effortless everyday style","subheading":"New-season apparel for women, men & kids — thoughtfully made, fairly priced.","buttonText":"Shop new in","buttonLink":"/products"}]'
WHERE th.TenantId = @tid AND tt.TemplateKey = 'index' AND ts.SectionType = 'Hero';

UPDATE ThemeSections ts
  JOIN ThemeTemplates tt ON tt.ThemeTemplateId = ts.ThemeTemplateId
  JOIN Themes th         ON th.ThemeId = tt.ThemeId
SET ts.Blocks = '[{"icon":"🚚","heading":"Free shipping","text":"On orders over ₹499"},{"icon":"↩️","heading":"Easy 30-day returns","text":"Free & hassle-free"},{"icon":"🧵","heading":"Quality craftsmanship","text":"Made to last"},{"icon":"💬","heading":"24/7 support","text":"We''re here to help"}]'
WHERE th.TenantId = @tid AND tt.TemplateKey = 'index' AND ts.SectionType = 'Multicolumn';

-- ---- 7. Summary -------------------------------------------------------------
SELECT
  (SELECT COUNT(*) FROM Categories WHERE TenantId = @tid AND Slug IN ('women','men','footwear','accessories','kids')) AS categories,
  (SELECT COUNT(*) FROM Products   WHERE TenantId = @tid AND Sku LIKE 'APP-%')                                        AS products,
  (SELECT COUNT(*) FROM Products   WHERE TenantId = @tid AND Sku LIKE 'APP-%' AND IsFeatured = 1)                     AS featured;
