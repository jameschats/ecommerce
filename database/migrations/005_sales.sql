-- =====================================================================
-- 005_sales.sql  —  Sales domain
-- Tables: Coupons, CouponUsage, Cart, CartItems, Orders, OrderItems,
--         OrderStatusHistory, Payments, PaymentTransactions, Refunds
-- Money is DECIMAL(12,2); line items snapshot price/name at order time.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Coupons` (
    `CouponId`          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`          BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Code`              VARCHAR(50)  NOT NULL,
    `Description`       VARCHAR(255) NULL,
    `DiscountType`      VARCHAR(20)  NOT NULL,         -- Flat | Percentage
    `DiscountValue`     DECIMAL(12,2) NOT NULL,
    `MaxDiscountAmount` DECIMAL(12,2) NULL,            -- cap for percentage coupons
    `MinOrderAmount`    DECIMAL(12,2) NULL,
    `UsageLimit`        INT NULL,
    `PerUserLimit`      INT NULL,
    `UsedCount`         INT NOT NULL DEFAULT 0,
    `StartsAt`          DATETIME NULL,
    `EndsAt`            DATETIME NULL,
    `IsActive`          TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`         DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`CouponId`),
    UNIQUE KEY `uq_coupons_tenant_code` (`TenantId`, `Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CouponUsage` (
    `CouponUsageId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `CouponId`       BIGINT UNSIGNED NOT NULL,
    `UserId`         BIGINT UNSIGNED NOT NULL,
    `OrderId`        BIGINT UNSIGNED NOT NULL,
    `DiscountAmount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`CouponUsageId`),
    KEY `ix_couponusage_coupon` (`CouponId`),
    KEY `ix_couponusage_user` (`UserId`),
    KEY `ix_couponusage_order` (`OrderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Cart` (
    `CartId`    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`  BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `UserId`    BIGINT UNSIGNED NULL,            -- NULL for guest carts
    `SessionId` VARCHAR(100) NULL,               -- guest session key
    `Status`    VARCHAR(20) NOT NULL DEFAULT 'Active',  -- Active | Converted | Abandoned
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`CartId`),
    KEY `ix_cart_user` (`UserId`),
    KEY `ix_cart_session` (`SessionId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CartItems` (
    `CartItemId`       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `CartId`           BIGINT UNSIGNED NOT NULL,
    `ProductId`        BIGINT UNSIGNED NOT NULL,
    `ProductVariantId` BIGINT UNSIGNED NULL,
    `Quantity`         INT NOT NULL DEFAULT 1,
    `UnitPrice`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,  -- snapshot
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`CartItemId`),
    UNIQUE KEY `uq_cartitems_cart_product_variant` (`CartId`, `ProductId`, `ProductVariantId`),
    KEY `ix_cartitems_product` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Orders` (
    `OrderId`           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`          BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `SellerId`          BIGINT UNSIGNED NULL,           -- future marketplace
    `UserId`            BIGINT UNSIGNED NOT NULL,
    `OrderNumber`       VARCHAR(40) NOT NULL,
    `Status`            VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending|Paid|Packed|Shipped|Delivered|Cancelled|Returned
    `CouponId`          BIGINT UNSIGNED NULL,
    `BillingAddressId`  BIGINT UNSIGNED NULL,
    `ShippingAddressId` BIGINT UNSIGNED NULL,
    `ShippingMethodId`  BIGINT UNSIGNED NULL,
    `Currency`          VARCHAR(3) NOT NULL DEFAULT 'INR',
    `Subtotal`          DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `DiscountAmount`    DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TaxAmount`         DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `ShippingAmount`    DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TotalAmount`       DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `Notes`             VARCHAR(500) NULL,
    `PlacedAt`          DATETIME NULL,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`         DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`OrderId`),
    UNIQUE KEY `uq_orders_number` (`OrderNumber`),
    KEY `ix_orders_user` (`UserId`),
    KEY `ix_orders_status` (`Status`),
    KEY `ix_orders_coupon` (`CouponId`),
    KEY `ix_orders_billing_addr` (`BillingAddressId`),
    KEY `ix_orders_shipping_addr` (`ShippingAddressId`),
    KEY `ix_orders_shipping_method` (`ShippingMethodId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `OrderItems` (
    `OrderItemId`      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `OrderId`          BIGINT UNSIGNED NOT NULL,
    `ProductId`        BIGINT UNSIGNED NOT NULL,
    `ProductVariantId` BIGINT UNSIGNED NULL,
    `Sku`              VARCHAR(64)  NULL,              -- snapshot
    `ProductName`      VARCHAR(250) NOT NULL,         -- snapshot
    `HsnCode`          VARCHAR(20)  NULL,
    `Quantity`         INT NOT NULL DEFAULT 1,
    `UnitPrice`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `DiscountAmount`   DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `TaxRate`          DECIMAL(5,2)  NOT NULL DEFAULT 0.00,
    `TaxAmount`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `LineTotal`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`OrderItemId`),
    KEY `ix_orderitems_order` (`OrderId`),
    KEY `ix_orderitems_product` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `OrderStatusHistory` (
    `OrderStatusHistoryId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `OrderId`              BIGINT UNSIGNED NOT NULL,
    `FromStatus`          VARCHAR(20) NULL,
    `ToStatus`            VARCHAR(20) NOT NULL,
    `Notes`               VARCHAR(255) NULL,
    `ChangedBy`           BIGINT UNSIGNED NULL,
    `CreatedAt`           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`OrderStatusHistoryId`),
    KEY `ix_orderstatushistory_order` (`OrderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Payments` (
    `PaymentId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`   BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `OrderId`    BIGINT UNSIGNED NOT NULL,
    `Method`     VARCHAR(20) NOT NULL,               -- Razorpay | COD
    `Status`     VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending|Success|Failed|Refunded|PartiallyRefunded
    `Amount`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `Currency`   VARCHAR(3) NOT NULL DEFAULT 'INR',
    `CreatedAt`  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`  DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`PaymentId`),
    KEY `ix_payments_order` (`OrderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `PaymentTransactions` (
    `PaymentTransactionId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `PaymentId`            BIGINT UNSIGNED NOT NULL,
    `Gateway`             VARCHAR(30) NOT NULL DEFAULT 'Razorpay',
    `GatewayOrderId`      VARCHAR(100) NULL,
    `GatewayPaymentId`    VARCHAR(100) NULL,
    `GatewaySignature`    VARCHAR(255) NULL,
    `TransactionType`     VARCHAR(20) NOT NULL,       -- Authorize | Capture | Refund
    `Amount`              DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `Status`              VARCHAR(20) NOT NULL,
    `RawResponse`         TEXT NULL,
    `CreatedAt`           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`PaymentTransactionId`),
    KEY `ix_paymenttxn_payment` (`PaymentId`),
    KEY `ix_paymenttxn_gateway_payment` (`GatewayPaymentId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Refunds` (
    `RefundId`        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `PaymentId`       BIGINT UNSIGNED NOT NULL,
    `OrderId`         BIGINT UNSIGNED NOT NULL,
    `Amount`          DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `Reason`          VARCHAR(255) NULL,
    `Status`          VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending | Processed | Failed
    `GatewayRefundId` VARCHAR(100) NULL,
    `ProcessedAt`     DATETIME NULL,
    `CreatedAt`       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`RefundId`),
    KEY `ix_refunds_payment` (`PaymentId`),
    KEY `ix_refunds_order` (`OrderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '005_sales.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '005_sales.sql');
