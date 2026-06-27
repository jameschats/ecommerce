-- =====================================================================
-- 007_billing.sql  —  Billing domain
-- Tables: Invoices, InvoiceItems, CreditNotes (future), CreditNoteItems (future)
-- Invoice is distinct from Payment: it carries GST split + billing identity.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Invoices` (
    `InvoiceId`      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `OrderId`        BIGINT UNSIGNED NOT NULL,
    `InvoiceNumber`  VARCHAR(40) NOT NULL,
    `InvoiceDate`    DATE NOT NULL,
    `BillingName`    VARCHAR(150) NULL,
    `BillingAddress` VARCHAR(500) NULL,
    `GstNumber`      VARCHAR(20)  NULL,
    `Subtotal`       DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TaxAmount`      DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `CgstAmount`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `SgstAmount`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `IgstAmount`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TotalAmount`    DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `PdfUrl`         VARCHAR(500) NULL,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`InvoiceId`),
    UNIQUE KEY `uq_invoices_number` (`InvoiceNumber`),
    KEY `ix_invoices_order` (`OrderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `InvoiceItems` (
    `InvoiceItemId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `InvoiceId`     BIGINT UNSIGNED NOT NULL,
    `ProductId`     BIGINT UNSIGNED NULL,
    `ProductName`   VARCHAR(250) NOT NULL,
    `HsnCode`       VARCHAR(20) NULL,
    `Quantity`      INT NOT NULL DEFAULT 1,
    `UnitPrice`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TaxRate`       DECIMAL(5,2)  NOT NULL DEFAULT 0.00,
    `TaxAmount`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `LineTotal`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `CreatedAt`     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`InvoiceItemId`),
    KEY `ix_invoiceitems_invoice` (`InvoiceId`),
    KEY `ix_invoiceitems_product` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CreditNotes` (
    `CreditNoteId`     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `InvoiceId`        BIGINT UNSIGNED NULL,
    `OrderId`          BIGINT UNSIGNED NULL,
    `CreditNoteNumber` VARCHAR(40) NOT NULL,
    `Reason`           VARCHAR(255) NULL,
    `Subtotal`         DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TaxAmount`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TotalAmount`      DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `Status`           VARCHAR(20) NOT NULL DEFAULT 'Draft',
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`CreditNoteId`),
    UNIQUE KEY `uq_creditnotes_number` (`CreditNoteNumber`),
    KEY `ix_creditnotes_invoice` (`InvoiceId`),
    KEY `ix_creditnotes_order` (`OrderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CreditNoteItems` (
    `CreditNoteItemId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `CreditNoteId`     BIGINT UNSIGNED NOT NULL,
    `ProductId`        BIGINT UNSIGNED NULL,
    `ProductName`      VARCHAR(250) NULL,
    `Quantity`         INT NOT NULL DEFAULT 1,
    `UnitPrice`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TaxAmount`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `LineTotal`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`CreditNoteItemId`),
    KEY `ix_creditnoteitems_creditnote` (`CreditNoteId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '007_billing.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '007_billing.sql');
