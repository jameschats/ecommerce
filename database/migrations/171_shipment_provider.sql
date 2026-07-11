-- =====================================================================
-- 171_shipment_provider.sql  —  V2 Shiprocket integration SR3.
-- Track which provider fulfilled a Shipment + the provider's ids so we can
-- schedule pickup, fetch the label and map tracking webhooks back. Manual
-- (Self) shipments keep Provider='Manual' and null provider ids. Additive.
-- =====================================================================

ALTER TABLE `Shipments`
    ADD COLUMN `Provider`           VARCHAR(20)  NOT NULL DEFAULT 'Manual' AFTER `ShippingMethodId`,
    ADD COLUMN `ProviderShipmentId` VARCHAR(50)  NULL AFTER `Provider`,
    ADD COLUMN `ProviderOrderId`    VARCHAR(50)  NULL AFTER `ProviderShipmentId`,
    ADD COLUMN `LabelUrl`           VARCHAR(500) NULL AFTER `ProviderOrderId`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '171_shipment_provider.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '171_shipment_provider.sql');
