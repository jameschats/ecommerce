-- =====================================================================
-- 289_sales_digest_app.sql  —  Second first-party app: "Sales Digest".
-- Periodic email summary of orders + revenue. Seeds the listed app. Idempotent.
-- =====================================================================

INSERT INTO `Apps` (`Name`, `Slug`, `ClientId`, `ClientSecretHash`, `SecretPrefix`, `Description`, `Category`,
                    `RedirectUris`, `RequestedScopes`, `IsEmbedded`, `PricingModel`, `Status`, `IsFirstParty`, `CreatedAt`)
SELECT 'Sales Digest', 'sales-digest', 'wcapp_salesdigest', '', '',
       'A daily or weekly email summarising your orders, revenue and top products.', 'Analytics',
       '', 'orders:read', 0, 'free', 'listed', 1, UTC_TIMESTAMP()
WHERE NOT EXISTS (SELECT 1 FROM `Apps` WHERE `Slug` = 'sales-digest');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '289_sales_digest_app.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '289_sales_digest_app.sql');
