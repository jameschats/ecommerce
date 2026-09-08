-- =====================================================================
-- 062_product_custom_fields.sql — Admin-defined "custom text" fields per
-- product (e.g. "Mention Correct Design number with quantity each"),
-- answered by the customer on the product page and carried onto the
-- order/invoice.
-- Forward-only; idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `ProductCustomFields` (
    `ProductCustomFieldId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`             BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `ProductId`            BIGINT UNSIGNED NOT NULL,
    `Label`                VARCHAR(200) NOT NULL,
    `CharLimit`            INT NOT NULL DEFAULT 500,
    `IsMandatory`          TINYINT(1) NOT NULL DEFAULT 0,
    `SortOrder`            INT NOT NULL DEFAULT 0,
    `CreatedAt`            DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`            DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ProductCustomFieldId`),
    KEY `ix_productcustomfields_product` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- What the customer actually typed, snapshotted onto the order item at sale time — editing
-- or deleting the field definition afterwards must not change what an already-placed order
-- shows.
CREATE TABLE IF NOT EXISTS `OrderItemCustomFieldValues` (
    `OrderItemCustomFieldValueId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `OrderItemId`                 BIGINT UNSIGNED NOT NULL,
    `Label`                       VARCHAR(200) NOT NULL,
    `Value`                       VARCHAR(1000) NOT NULL,
    `CreatedAt`                   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`OrderItemCustomFieldValueId`),
    KEY `ix_orderitemcustomfieldvalues_item` (`OrderItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Same snapshot, copied onto the invoice item when the invoice is generated — InvoiceItems
-- already duplicates ProductName/DesignNo/HsnCode rather than referencing OrderItems, so this
-- follows the same convention.
CREATE TABLE IF NOT EXISTS `InvoiceItemCustomFieldValues` (
    `InvoiceItemCustomFieldValueId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `InvoiceItemId`                 BIGINT UNSIGNED NOT NULL,
    `Label`                         VARCHAR(200) NOT NULL,
    `Value`                         VARCHAR(1000) NOT NULL,
    `CreatedAt`                     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`InvoiceItemCustomFieldValueId`),
    KEY `ix_invoiceitemcustomfieldvalues_item` (`InvoiceItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '062_product_custom_fields.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '062_product_custom_fields.sql');
