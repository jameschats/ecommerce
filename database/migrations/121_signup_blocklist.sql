-- =====================================================================
-- 121_signup_blocklist.sql  —  V2-3: block re-signup of bad actors by
-- email / GSTIN / phone (super-admin governance). Additive (V2 120-129).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `SignupBlocklist` (
    `SignupBlocklistId` BIGINT       NOT NULL AUTO_INCREMENT,
    `Type`              VARCHAR(20)  NOT NULL,        -- Email | Gstin | Phone
    `Value`             VARCHAR(255) NOT NULL,        -- normalized (lowercase for email)
    `Reason`            VARCHAR(500) NULL,
    `CreatedByAdminId`  BIGINT       NULL,
    `CreatedAt`         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SignupBlocklistId`),
    UNIQUE KEY `ux_blocklist_type_value` (`Type`, `Value`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '121_signup_blocklist.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '121_signup_blocklist.sql');
