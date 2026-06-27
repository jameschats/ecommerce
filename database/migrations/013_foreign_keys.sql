-- =====================================================================
-- 013_foreign_keys.sql  —  All cross-domain foreign keys
-- Added after every table exists, so create-order never matters.
-- Forward-only (MySQL has no ADD CONSTRAINT IF NOT EXISTS); the runner
-- applies this once via __schema_migrations.
--
-- NOTE: TenantId columns are intentionally NOT FK-constrained yet —
-- multi-tenancy is a V2 concern; they default to 1 in V1.
-- Delete rules: child/detail rows CASCADE; optional links SET NULL;
-- order history (OrderItems.ProductId) RESTRICTs to preserve records.
-- =====================================================================

-- Catalog -------------------------------------------------------------
ALTER TABLE `Products`
    ADD CONSTRAINT `fk_products_category` FOREIGN KEY (`CategoryId`) REFERENCES `Categories` (`CategoryId`) ON DELETE RESTRICT,
    ADD CONSTRAINT `fk_products_brand`    FOREIGN KEY (`BrandId`)    REFERENCES `Brands` (`BrandId`)       ON DELETE SET NULL,
    ADD CONSTRAINT `fk_products_taxrate`  FOREIGN KEY (`TaxRateId`)  REFERENCES `TaxRates` (`TaxRateId`)   ON DELETE SET NULL,
    ADD CONSTRAINT `fk_products_seller`   FOREIGN KEY (`SellerId`)   REFERENCES `Sellers` (`SellerId`)     ON DELETE SET NULL;

ALTER TABLE `ProductImages`
    ADD CONSTRAINT `fk_productimages_product` FOREIGN KEY (`ProductId`)   REFERENCES `Products` (`ProductId`)     ON DELETE CASCADE,
    ADD CONSTRAINT `fk_productimages_media`   FOREIGN KEY (`MediaFileId`) REFERENCES `MediaFiles` (`MediaFileId`) ON DELETE SET NULL;

ALTER TABLE `ProductVariants`
    ADD CONSTRAINT `fk_productvariants_product` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`ProductId`) ON DELETE CASCADE;

ALTER TABLE `VariantOptions`
    ADD CONSTRAINT `fk_variantoptions_variant` FOREIGN KEY (`ProductVariantId`) REFERENCES `ProductVariants` (`ProductVariantId`) ON DELETE CASCADE;

ALTER TABLE `AttributeValues`
    ADD CONSTRAINT `fk_attributevalues_attribute` FOREIGN KEY (`AttributeId`) REFERENCES `Attributes` (`AttributeId`) ON DELETE CASCADE;

ALTER TABLE `ProductAttributeValues`
    ADD CONSTRAINT `fk_pav_product`   FOREIGN KEY (`ProductId`)        REFERENCES `Products` (`ProductId`)             ON DELETE CASCADE,
    ADD CONSTRAINT `fk_pav_attribute` FOREIGN KEY (`AttributeId`)      REFERENCES `Attributes` (`AttributeId`)         ON DELETE CASCADE,
    ADD CONSTRAINT `fk_pav_value`     FOREIGN KEY (`AttributeValueId`) REFERENCES `AttributeValues` (`AttributeValueId`) ON DELETE SET NULL;

-- Inventory -----------------------------------------------------------
ALTER TABLE `Inventory`
    ADD CONSTRAINT `fk_inventory_product` FOREIGN KEY (`ProductId`)        REFERENCES `Products` (`ProductId`)               ON DELETE CASCADE,
    ADD CONSTRAINT `fk_inventory_variant` FOREIGN KEY (`ProductVariantId`) REFERENCES `ProductVariants` (`ProductVariantId`) ON DELETE CASCADE;

ALTER TABLE `InventoryTransactions`
    ADD CONSTRAINT `fk_invtxn_inventory` FOREIGN KEY (`InventoryId`) REFERENCES `Inventory` (`InventoryId`) ON DELETE CASCADE,
    ADD CONSTRAINT `fk_invtxn_product`   FOREIGN KEY (`ProductId`)   REFERENCES `Products` (`ProductId`)   ON DELETE CASCADE;

-- Sales ---------------------------------------------------------------
ALTER TABLE `CouponUsage`
    ADD CONSTRAINT `fk_couponusage_coupon` FOREIGN KEY (`CouponId`) REFERENCES `Coupons` (`CouponId`) ON DELETE CASCADE,
    ADD CONSTRAINT `fk_couponusage_user`   FOREIGN KEY (`UserId`)   REFERENCES `Users` (`UserId`)     ON DELETE CASCADE,
    ADD CONSTRAINT `fk_couponusage_order`  FOREIGN KEY (`OrderId`)  REFERENCES `Orders` (`OrderId`)   ON DELETE CASCADE;

ALTER TABLE `Cart`
    ADD CONSTRAINT `fk_cart_user` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`) ON DELETE CASCADE;

ALTER TABLE `CartItems`
    ADD CONSTRAINT `fk_cartitems_cart`    FOREIGN KEY (`CartId`)           REFERENCES `Cart` (`CartId`)                     ON DELETE CASCADE,
    ADD CONSTRAINT `fk_cartitems_product` FOREIGN KEY (`ProductId`)        REFERENCES `Products` (`ProductId`)              ON DELETE CASCADE,
    ADD CONSTRAINT `fk_cartitems_variant` FOREIGN KEY (`ProductVariantId`) REFERENCES `ProductVariants` (`ProductVariantId`) ON DELETE CASCADE;

ALTER TABLE `Orders`
    ADD CONSTRAINT `fk_orders_user`            FOREIGN KEY (`UserId`)            REFERENCES `Users` (`UserId`)                       ON DELETE RESTRICT,
    ADD CONSTRAINT `fk_orders_coupon`          FOREIGN KEY (`CouponId`)          REFERENCES `Coupons` (`CouponId`)                   ON DELETE SET NULL,
    ADD CONSTRAINT `fk_orders_billing_addr`    FOREIGN KEY (`BillingAddressId`)  REFERENCES `CustomerAddresses` (`CustomerAddressId`) ON DELETE SET NULL,
    ADD CONSTRAINT `fk_orders_shipping_addr`   FOREIGN KEY (`ShippingAddressId`) REFERENCES `CustomerAddresses` (`CustomerAddressId`) ON DELETE SET NULL,
    ADD CONSTRAINT `fk_orders_shipping_method` FOREIGN KEY (`ShippingMethodId`)  REFERENCES `ShippingMethods` (`ShippingMethodId`)   ON DELETE SET NULL,
    ADD CONSTRAINT `fk_orders_seller`          FOREIGN KEY (`SellerId`)          REFERENCES `Sellers` (`SellerId`)                   ON DELETE SET NULL;

ALTER TABLE `OrderItems`
    ADD CONSTRAINT `fk_orderitems_order`   FOREIGN KEY (`OrderId`)          REFERENCES `Orders` (`OrderId`)                   ON DELETE CASCADE,
    ADD CONSTRAINT `fk_orderitems_product` FOREIGN KEY (`ProductId`)        REFERENCES `Products` (`ProductId`)               ON DELETE RESTRICT,
    ADD CONSTRAINT `fk_orderitems_variant` FOREIGN KEY (`ProductVariantId`) REFERENCES `ProductVariants` (`ProductVariantId`) ON DELETE SET NULL;

ALTER TABLE `OrderStatusHistory`
    ADD CONSTRAINT `fk_orderstatushistory_order` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`OrderId`) ON DELETE CASCADE;

ALTER TABLE `Payments`
    ADD CONSTRAINT `fk_payments_order` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`OrderId`) ON DELETE CASCADE;

ALTER TABLE `PaymentTransactions`
    ADD CONSTRAINT `fk_paymenttxn_payment` FOREIGN KEY (`PaymentId`) REFERENCES `Payments` (`PaymentId`) ON DELETE CASCADE;

ALTER TABLE `Refunds`
    ADD CONSTRAINT `fk_refunds_payment` FOREIGN KEY (`PaymentId`) REFERENCES `Payments` (`PaymentId`) ON DELETE CASCADE,
    ADD CONSTRAINT `fk_refunds_order`   FOREIGN KEY (`OrderId`)   REFERENCES `Orders` (`OrderId`)     ON DELETE CASCADE;

-- Shipping ------------------------------------------------------------
ALTER TABLE `ShippingZones`
    ADD CONSTRAINT `fk_shippingzones_method` FOREIGN KEY (`ShippingMethodId`) REFERENCES `ShippingMethods` (`ShippingMethodId`) ON DELETE SET NULL;

ALTER TABLE `Shipments`
    ADD CONSTRAINT `fk_shipments_order`  FOREIGN KEY (`OrderId`)          REFERENCES `Orders` (`OrderId`)                 ON DELETE CASCADE,
    ADD CONSTRAINT `fk_shipments_method` FOREIGN KEY (`ShippingMethodId`) REFERENCES `ShippingMethods` (`ShippingMethodId`) ON DELETE SET NULL;

-- Billing -------------------------------------------------------------
ALTER TABLE `Invoices`
    ADD CONSTRAINT `fk_invoices_order` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`OrderId`) ON DELETE CASCADE;

ALTER TABLE `InvoiceItems`
    ADD CONSTRAINT `fk_invoiceitems_invoice` FOREIGN KEY (`InvoiceId`) REFERENCES `Invoices` (`InvoiceId`) ON DELETE CASCADE,
    ADD CONSTRAINT `fk_invoiceitems_product` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`ProductId`) ON DELETE SET NULL;

ALTER TABLE `CreditNotes`
    ADD CONSTRAINT `fk_creditnotes_invoice` FOREIGN KEY (`InvoiceId`) REFERENCES `Invoices` (`InvoiceId`) ON DELETE SET NULL,
    ADD CONSTRAINT `fk_creditnotes_order`   FOREIGN KEY (`OrderId`)   REFERENCES `Orders` (`OrderId`)     ON DELETE SET NULL;

ALTER TABLE `CreditNoteItems`
    ADD CONSTRAINT `fk_creditnoteitems_creditnote` FOREIGN KEY (`CreditNoteId`) REFERENCES `CreditNotes` (`CreditNoteId`) ON DELETE CASCADE,
    ADD CONSTRAINT `fk_creditnoteitems_product`    FOREIGN KEY (`ProductId`)    REFERENCES `Products` (`ProductId`)       ON DELETE SET NULL;

-- Customer ------------------------------------------------------------
ALTER TABLE `CustomerAddresses`
    ADD CONSTRAINT `fk_customeraddresses_user` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`) ON DELETE CASCADE;

ALTER TABLE `Reviews`
    ADD CONSTRAINT `fk_reviews_product` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`ProductId`) ON DELETE CASCADE,
    ADD CONSTRAINT `fk_reviews_user`    FOREIGN KEY (`UserId`)    REFERENCES `Users` (`UserId`)       ON DELETE CASCADE,
    ADD CONSTRAINT `fk_reviews_order`   FOREIGN KEY (`OrderId`)   REFERENCES `Orders` (`OrderId`)     ON DELETE SET NULL;

-- CMS -----------------------------------------------------------------
ALTER TABLE `PageSections`
    ADD CONSTRAINT `fk_pagesections_page` FOREIGN KEY (`PageId`) REFERENCES `Pages` (`PageId`) ON DELETE CASCADE;

ALTER TABLE `SectionConfigurations`
    ADD CONSTRAINT `fk_sectionconfigurations_section` FOREIGN KEY (`PageSectionId`) REFERENCES `PageSections` (`PageSectionId`) ON DELETE CASCADE;

-- Theme ---------------------------------------------------------------
ALTER TABLE `ThemeSettings`
    ADD CONSTRAINT `fk_themesettings_theme` FOREIGN KEY (`ThemeId`) REFERENCES `Themes` (`ThemeId`) ON DELETE CASCADE;

-- Platform ------------------------------------------------------------
ALTER TABLE `MediaFiles`
    ADD CONSTRAINT `fk_mediafiles_folder` FOREIGN KEY (`MediaFolderId`) REFERENCES `MediaFolders` (`MediaFolderId`) ON DELETE SET NULL;

ALTER TABLE `NotificationHistory`
    ADD CONSTRAINT `fk_notifhistory_template` FOREIGN KEY (`TemplateId`) REFERENCES `NotificationTemplates` (`NotificationTemplateId`) ON DELETE SET NULL;

ALTER TABLE `AuditLogs`
    ADD CONSTRAINT `fk_auditlogs_user` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`) ON DELETE SET NULL;

ALTER TABLE `ImportJobItems`
    ADD CONSTRAINT `fk_importjobitems_job` FOREIGN KEY (`ImportJobId`) REFERENCES `ImportJobs` (`ImportJobId`) ON DELETE CASCADE;

-- Marketplace (future) ------------------------------------------------
ALTER TABLE `SellerUsers`
    ADD CONSTRAINT `fk_sellerusers_seller` FOREIGN KEY (`SellerId`) REFERENCES `Sellers` (`SellerId`) ON DELETE CASCADE,
    ADD CONSTRAINT `fk_sellerusers_user`   FOREIGN KEY (`UserId`)   REFERENCES `Users` (`UserId`)     ON DELETE CASCADE;

ALTER TABLE `SellerCommissions`
    ADD CONSTRAINT `fk_sellercommissions_seller`    FOREIGN KEY (`SellerId`)    REFERENCES `Sellers` (`SellerId`)       ON DELETE CASCADE,
    ADD CONSTRAINT `fk_sellercommissions_order`     FOREIGN KEY (`OrderId`)     REFERENCES `Orders` (`OrderId`)         ON DELETE CASCADE,
    ADD CONSTRAINT `fk_sellercommissions_orderitem` FOREIGN KEY (`OrderItemId`) REFERENCES `OrderItems` (`OrderItemId`) ON DELETE SET NULL;

ALTER TABLE `SellerSettlements`
    ADD CONSTRAINT `fk_sellersettlements_seller` FOREIGN KEY (`SellerId`) REFERENCES `Sellers` (`SellerId`) ON DELETE CASCADE;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '013_foreign_keys.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '013_foreign_keys.sql');
