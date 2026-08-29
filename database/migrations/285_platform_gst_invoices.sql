-- =====================================================================
-- 285_platform_gst_invoices.sql  —  Billing D1: GST tax invoices for the SaaS fee.
-- The platform issues a GST tax invoice to each merchant per subscription charge.
-- PlatformBillingSettings = the platform's seller/GSTIN details (one global row);
-- PlatformInvoices = the issued invoices (global numbering per financial year).
-- Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `PlatformBillingSettings` (
  `PlatformBillingSettingsId` INT NOT NULL AUTO_INCREMENT,
  `SellerLegalName` VARCHAR(200) NULL,
  `SellerGstin`     VARCHAR(20) NULL,
  `SellerAddress`   VARCHAR(400) NULL,
  `SellerState`     VARCHAR(80) NULL,
  `GstRatePercent`  DECIMAL(5,2) NOT NULL DEFAULT 18.00,
  `InvoicePrefix`   VARCHAR(16) NOT NULL DEFAULT 'INV',
  `UpdatedAt`       DATETIME NULL,
  PRIMARY KEY (`PlatformBillingSettingsId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed a single placeholder row (real GSTIN/name/state set in super-admin before go-live).
INSERT INTO `PlatformBillingSettings` (`SellerLegalName`, `SellerGstin`, `SellerAddress`, `SellerState`, `GstRatePercent`, `InvoicePrefix`)
SELECT 'WavCommerce', NULL, NULL, NULL, 18.00, 'WAV'
WHERE NOT EXISTS (SELECT 1 FROM `PlatformBillingSettings`);

CREATE TABLE IF NOT EXISTS `PlatformInvoices` (
  `PlatformInvoiceId`       BIGINT NOT NULL AUTO_INCREMENT,
  `TenantId`                BIGINT NOT NULL,
  `TenantBillingHistoryId`  BIGINT NOT NULL,
  `FinancialYear`           VARCHAR(9) NOT NULL,
  `SequenceNumber`          INT NOT NULL,
  `InvoiceNumber`           VARCHAR(40) NOT NULL,
  `InvoiceDate`             DATETIME NOT NULL,
  `SellerName`              VARCHAR(200) NULL,
  `SellerGstin`             VARCHAR(20) NULL,
  `SellerState`             VARCHAR(80) NULL,
  `BuyerName`               VARCHAR(200) NULL,
  `BuyerGstin`              VARCHAR(20) NULL,
  `BuyerState`              VARCHAR(80) NULL,
  `PlaceOfSupply`           VARCHAR(80) NOT NULL DEFAULT '',
  `IsInterState`            TINYINT(1) NOT NULL DEFAULT 0,
  `GstRatePercent`          DECIMAL(5,2) NOT NULL DEFAULT 18.00,
  `TaxableValue`            DECIMAL(12,2) NOT NULL DEFAULT 0,
  `CgstAmount`              DECIMAL(12,2) NOT NULL DEFAULT 0,
  `SgstAmount`              DECIMAL(12,2) NOT NULL DEFAULT 0,
  `IgstAmount`              DECIMAL(12,2) NOT NULL DEFAULT 0,
  `TotalAmount`             DECIMAL(12,2) NOT NULL DEFAULT 0,
  `CreatedAt`               DATETIME NOT NULL,
  PRIMARY KEY (`PlatformInvoiceId`),
  UNIQUE KEY `UQ_PlatformInvoice_Charge` (`TenantBillingHistoryId`),
  UNIQUE KEY `UQ_PlatformInvoice_Number` (`InvoiceNumber`),
  KEY `IX_PlatformInvoice_Tenant` (`TenantId`),
  KEY `IX_PlatformInvoice_FY_Seq` (`FinancialYear`, `SequenceNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '285_platform_gst_invoices.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '285_platform_gst_invoices.sql');
