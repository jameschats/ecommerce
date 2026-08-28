-- =====================================================================
-- 279_growth_festivals.sql  —  AI Growth M2 (G3): the marketing calendar's
-- Indian festival / commerce-occasion list. GLOBAL (not tenant-scoped) — the
-- same calendar for every store, like Role/Permission. Dates are indicative
-- (lunar festivals shift year to year); they drive lead-time nudges, not orders.
-- Idempotent: table created if missing, rows inserted only if absent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `GrowthFestivals` (
  `GrowthFestivalId` BIGINT NOT NULL AUTO_INCREMENT,
  `Name`            VARCHAR(120) NOT NULL,
  `Date`            DATE NOT NULL,
  `Region`          VARCHAR(60) NOT NULL DEFAULT 'All India',
  `SuggestedGoal`   VARCHAR(40) NOT NULL DEFAULT 'festival',
  `Note`            VARCHAR(255) NULL,
  PRIMARY KEY (`GrowthFestivalId`),
  UNIQUE KEY `UQ_GrowthFestival_Name_Date` (`Name`, `Date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO `GrowthFestivals` (`Name`, `Date`, `Region`, `SuggestedGoal`, `Note`) VALUES
-- 2026 H2
('Raksha Bandhan',        '2026-08-28', 'All India',   'festival', 'Gifting for siblings.'),
('Janmashtami',           '2026-09-04', 'All India',   'festival', NULL),
('Ganesh Chaturthi',      '2026-09-14', 'West India',  'festival', 'Big in Maharashtra & the South.'),
('Festive Sale Season',   '2026-10-01', 'All India',   'weekend-sale', 'The big online-shopping window kicks off.'),
('Navratri begins',       '2026-10-11', 'All India',   'festival', '9 nights — apparel & jewellery peak.'),
('Durga Puja',            '2026-10-17', 'East India',  'festival', NULL),
('Dussehra (Vijayadashami)','2026-10-20','All India',  'festival', NULL),
('Karva Chauth',          '2026-10-28', 'North India', 'festival', 'Jewellery & gifting.'),
('Dhanteras',             '2026-11-06', 'All India',   'festival', 'Auspicious for buying — gold, utensils, electronics.'),
('Diwali (Deepavali)',    '2026-11-08', 'All India',   'festival', 'The biggest sale event of the year.'),
('Bhai Dooj',             '2026-11-10', 'All India',   'festival', NULL),
('Children''s Day',       '2026-11-14', 'All India',   'festival', 'Toys, kids apparel & books.'),
('Chhath Puja',           '2026-11-15', 'North India', 'festival', 'Bihar, Jharkhand, eastern UP.'),
('Guru Nanak Jayanti',    '2026-11-24', 'All India',   'festival', NULL),
('Christmas',             '2026-12-25', 'All India',   'festival', NULL),
('Year-End Sale',         '2026-12-26', 'All India',   'clearance', 'Clear stock before the new year.'),
('New Year''s Eve',       '2026-12-31', 'All India',   'weekend-sale', NULL),
-- 2027
('New Year',              '2027-01-01', 'All India',   'festival', NULL),
('Pongal / Makar Sankranti','2027-01-14','South India','festival', 'Harvest festival — Pongal (South), Sankranti/Lohri (North).'),
('Republic Day',          '2027-01-26', 'All India',   'weekend-sale', 'Republic Day sales are a strong window.'),
('Vasant Panchami',       '2027-02-01', 'All India',   'festival', NULL),
('Valentine''s Day',      '2027-02-14', 'All India',   'festival', 'Gifting, jewellery, flowers.'),
('Maha Shivratri',        '2027-03-06', 'All India',   'festival', NULL),
('Eid al-Fitr',           '2027-03-10', 'All India',   'festival', 'Date is approximate (moon sighting).'),
('Holi',                  '2027-03-22', 'All India',   'festival', 'Colours, sweets, apparel.'),
('Ugadi / Gudi Padwa',    '2027-04-07', 'South India', 'festival', 'New year in the South & Maharashtra.'),
('Baisakhi',              '2027-04-14', 'North India', 'festival', 'Punjab harvest festival.'),
('Ram Navami',            '2027-04-15', 'All India',   'festival', NULL),
('Akshaya Tritiya',       '2027-05-09', 'All India',   'festival', 'Most auspicious day to buy gold.'),
('Mother''s Day',         '2027-05-09', 'All India',   'festival', 'Gifting.'),
('Eid al-Adha (Bakrid)',  '2027-05-17', 'All India',   'festival', 'Date is approximate (moon sighting).'),
('Father''s Day',         '2027-06-20', 'All India',   'festival', 'Gifting.'),
('Friendship Day',        '2027-08-01', 'All India',   'festival', NULL),
('Independence Day',      '2027-08-15', 'All India',   'weekend-sale', 'Independence Day sales.'),
('Raksha Bandhan',        '2027-08-17', 'All India',   'festival', 'Gifting for siblings.');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '279_growth_festivals.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '279_growth_festivals.sql');
