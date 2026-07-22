-- =====================================================================
-- 253_shipment_checkpoints.sql  —  Stop discarding courier tracking scans (C2).
--
-- ShiprocketWebhookService.Map() returns (null, null, false) for anything it
-- doesn't recognise and the handler then does nothing — so every intermediate
-- scan ("Reached destination hub", "Out for delivery", failed delivery attempts)
-- is dropped on the floor. Only the current Shipment.Status survives, and each
-- webhook overwrites it. That data is unrecoverable once discarded: the courier
-- does not resend history.
--
-- This is an append-only log of every webhook we receive, mapped or not, so the
-- order timeline can show a real journey and a support bot can answer "where is
-- it, and since when". Raw payload is kept for the ones we couldn't map — that
-- is how the mapping gets improved later without guessing.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `ShipmentCheckpoints` (
    `ShipmentCheckpointId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`             BIGINT       NOT NULL DEFAULT 1,
    `ShipmentId`           BIGINT       NOT NULL,
    `RawStatus`            VARCHAR(150) NOT NULL,          -- exactly what the courier sent
    `MappedStatus`         VARCHAR(30)  NULL,              -- our lifecycle value, NULL when unrecognised
    `Location`             VARCHAR(150) NULL,
    `Remark`               VARCHAR(300) NULL,
    `OccurredAt`           DATETIME     NULL,              -- courier's timestamp when supplied
    `RawPayload`           TEXT         NULL,              -- kept only for unmapped statuses
    `CreatedAt`            DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`ShipmentCheckpointId`),
    KEY `IX_ShipmentCheckpoints_Shipment` (`ShipmentId`, `ShipmentCheckpointId`),
    KEY `IX_ShipmentCheckpoints_Tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '253_shipment_checkpoints.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '253_shipment_checkpoints.sql');
