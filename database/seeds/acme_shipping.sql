-- =============================================================================
-- Minimal shipping config for the "acme" store so checkout is serviceable.
-- One active flat-rate method → every pincode is serviceable at the flat rate
-- (₹49, free over ₹499). Zones are only needed to RESTRICT/override, so none here.
-- Idempotent: only inserts when the store has no active method yet (won't clobber
-- anything you later add via /admin/shipping).
--
-- Run:  mysql -h 127.0.0.1 -P 3306 --protocol=TCP -u <user> -p<pwd> <db> \
--         --init-command="SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci" \
--         < database/seeds/acme_shipping.sql
-- =============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET @slug := 'acme';
SET @tid  := (SELECT TenantId FROM Tenants WHERE Slug = @slug LIMIT 1);
SELECT @tid AS resolved_tenant_id;

INSERT INTO ShippingMethods (TenantId, Name, Description, RateType, BaseRate, FreeShippingThreshold, EstimatedDays, IsActive)
SELECT @tid, 'Standard Delivery', 'Flat-rate delivery — free over ₹499', 'Flat', 49.00, 499.00, 5, 1
WHERE @tid IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM ShippingMethods WHERE TenantId = @tid AND IsActive = 1);

SELECT (SELECT COUNT(*) FROM ShippingMethods WHERE TenantId = @tid AND IsActive = 1) AS active_methods;
