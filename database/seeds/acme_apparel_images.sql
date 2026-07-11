-- =============================================================================
-- Real apparel photos for the acme store (verified Unsplash/Pexels CDN URLs).
-- Replaces the placehold.co placeholders seeded by acme_apparel_demo.sql.
-- Idempotent: re-running just re-sets the same URLs. Run AFTER the demo seed.
-- =============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET @slug := 'acme';
SET @tid  := (SELECT TenantId FROM Tenants WHERE Slug = @slug LIMIT 1);
SELECT @tid AS resolved_tenant_id;

-- ---- Product images (primary) ----
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1595777457583-95e059d581b8?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-W-001' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1564257631407-4deb1f99d992?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-W-002' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1541099649105-f69ad21f3246?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-W-003' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.pexels.com/photos/8839887/pexels-photo-8839887.jpeg?auto=compress&cs=tinysrgb&w=700' WHERE p.TenantId = @tid AND p.Sku = 'APP-W-004' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1602810318383-e386cc2a3ccf?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-M-001' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-M-002' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1473966968600-fa801b869a1a?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-M-003' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1551028719-00167b16eac5?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-M-004' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1600185365483-26d7a4cc7519?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-F-001' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1533867617858-e7b97e060509?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-F-002' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1595950653106-6c9ebd614d3a?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-F-003' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1543163521-1bf539c55dd2?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-F-004' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1584917865442-de89df76afd3?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-A-001' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1511499767150-a48a237f0083?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-A-002' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1524592094714-0f0654e20314?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-A-003' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1624222247344-550fb60583dc?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-A-004' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-K-001' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1503919545889-aef636e10ad4?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-K-002' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1622290291468-a28f7a7dc6a8?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-K-003' AND pi.IsPrimary = 1;
UPDATE ProductImages pi JOIN Products p ON p.ProductId = pi.ProductId SET pi.Url = 'https://images.unsplash.com/photo-1518831959646-742c3a14ebf7?auto=format&fit=crop&w=700&q=80' WHERE p.TenantId = @tid AND p.Sku = 'APP-K-004' AND pi.IsPrimary = 1;

-- ---- Category card images ----
UPDATE Categories SET ImageUrl = 'https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=700&q=80' WHERE TenantId = @tid AND Slug = 'women';
UPDATE Categories SET ImageUrl = 'https://images.unsplash.com/photo-1516257984-b1b4d707412e?auto=format&fit=crop&w=700&q=80' WHERE TenantId = @tid AND Slug = 'men';
UPDATE Categories SET ImageUrl = 'https://images.unsplash.com/photo-1549298916-b41d501d3772?auto=format&fit=crop&w=700&q=80' WHERE TenantId = @tid AND Slug = 'footwear';
UPDATE Categories SET ImageUrl = 'https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=700&q=80' WHERE TenantId = @tid AND Slug = 'accessories';
UPDATE Categories SET ImageUrl = 'https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?auto=format&fit=crop&w=700&q=80' WHERE TenantId = @tid AND Slug = 'kids';

-- ---- Home hero banner image (apparel copy + real photo) ----
UPDATE ThemeSections ts JOIN ThemeTemplates tt ON tt.ThemeTemplateId = ts.ThemeTemplateId JOIN Themes th ON th.ThemeId = tt.ThemeId
SET ts.Blocks = '[{"image":"https://images.unsplash.com/photo-1441984904996-e0b6ba687e04?auto=format&fit=crop&w=1600&q=80","heading":"Effortless everyday style","subheading":"New-season apparel for women, men & kids — thoughtfully made, fairly priced.","buttonText":"Shop new in","buttonLink":"/products"}]'
WHERE th.TenantId = @tid AND tt.TemplateKey = 'index' AND ts.SectionType = 'Hero';

-- ---- Summary ----
SELECT (SELECT COUNT(*) FROM ProductImages pi JOIN Products p ON p.ProductId=pi.ProductId WHERE p.TenantId=@tid AND p.Sku LIKE 'APP-%' AND pi.Url LIKE 'https://images.%') AS product_photos,
       (SELECT COUNT(*) FROM Categories WHERE TenantId=@tid AND ImageUrl LIKE 'https://images.%') AS category_photos;
