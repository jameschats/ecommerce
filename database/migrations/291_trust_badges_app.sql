-- =====================================================================
-- 291_trust_badges_app.sql  —  App Store S5 demo: "Trust Badges" app that
-- provides a storefront theme block. Seeds the listed first-party app (no API
-- scopes needed — it only contributes a theme section). Idempotent.
-- =====================================================================

INSERT INTO `Apps` (`Name`, `Slug`, `ClientId`, `ClientSecretHash`, `SecretPrefix`, `Description`, `Category`,
                    `RedirectUris`, `RequestedScopes`, `IsEmbedded`, `PricingModel`, `Status`, `IsFirstParty`, `CreatedAt`)
SELECT 'Trust Badges', 'trust-badges', 'wcapp_trustbadges', '', '',
       'Add a row of trust signals (secure payments, easy returns…) to your storefront theme.', 'Storefront',
       '', '', 0, 'free', 'listed', 1, UTC_TIMESTAMP()
WHERE NOT EXISTS (SELECT 1 FROM `Apps` WHERE `Slug` = 'trust-badges');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '291_trust_badges_app.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '291_trust_badges_app.sql');
