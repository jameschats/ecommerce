-- ---------------------------------------------------------------------------
-- 032_manual_payment.sql — manual UPI / bank-transfer payment
--
-- See documents/stages-v2/design.md §8. There is no gateway: the buyer pays with their
-- own app and reports the reference; an admin confirms the money arrived.
--
-- The buyer's claim and the admin's confirmation are stored separately on purpose —
-- ReportedAt is what the customer said, ConfirmedAt is what the shop verified. Collapsing
-- them into one field would make "they say they paid" indistinguishable from "we checked".
-- ---------------------------------------------------------------------------

SET @s := DATABASE();

-- ReferenceNumber — the UPI/UTR reference the buyer gives us
SET @c := (SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema=@s AND table_name='Payments' AND column_name='ReferenceNumber');
SET @q := IF(@c=0, 'ALTER TABLE `Payments` ADD COLUMN `ReferenceNumber` VARCHAR(100) NULL', 'SELECT 1');
PREPARE st FROM @q; EXECUTE st; DEALLOCATE PREPARE st;

-- ProofImageUrl — optional screenshot of the payment
SET @c := (SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema=@s AND table_name='Payments' AND column_name='ProofImageUrl');
SET @q := IF(@c=0, 'ALTER TABLE `Payments` ADD COLUMN `ProofImageUrl` VARCHAR(500) NULL', 'SELECT 1');
PREPARE st FROM @q; EXECUTE st; DEALLOCATE PREPARE st;

-- ReportedAt — when the BUYER claimed to have paid
SET @c := (SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema=@s AND table_name='Payments' AND column_name='ReportedAt');
SET @q := IF(@c=0, 'ALTER TABLE `Payments` ADD COLUMN `ReportedAt` DATETIME NULL', 'SELECT 1');
PREPARE st FROM @q; EXECUTE st; DEALLOCATE PREPARE st;

-- ConfirmedAt / ConfirmedBy — when the SHOP verified it, and who
SET @c := (SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema=@s AND table_name='Payments' AND column_name='ConfirmedAt');
SET @q := IF(@c=0, 'ALTER TABLE `Payments` ADD COLUMN `ConfirmedAt` DATETIME NULL', 'SELECT 1');
PREPARE st FROM @q; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema=@s AND table_name='Payments' AND column_name='ConfirmedBy');
SET @q := IF(@c=0, 'ALTER TABLE `Payments` ADD COLUMN `ConfirmedBy` BIGINT NULL', 'SELECT 1');
PREPARE st FROM @q; EXECUTE st; DEALLOCATE PREPARE st;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '032_manual_payment.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '032_manual_payment.sql');
