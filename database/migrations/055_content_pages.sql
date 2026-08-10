-- ---------------------------------------------------------------------------
-- 055_content_pages.sql — editable content for About, FAQ, Buying guide, Contact
--
-- The CMS could arrange the home page but could not hold a word of text:
-- PageSections has SectionType, Title, ordering, visibility and scheduling, and no
-- content column at all. "CustomHtml" is named in a code comment with nowhere to
-- put the HTML. Meanwhile About, FAQ and Contact were hardcoded Angular components
-- and Buying guide did not exist.
--
-- Sections are typed rather than one blob of HTML per page, because About is not
-- prose: it is an intro, a grid of statistics, a row of value cards and a call to
-- action. Flattening that into a single rich-text field would make it "editable"
-- by destroying it. Types:
--
--   Prose  Title = heading, Content = rich HTML
--   Faq    Title = question, Content = answer
--   Stats  Content = JSON [{ value, label }]
--   Cards  Content = JSON [{ icon, title, text }]
--   Cta    Content = JSON { heading, text, buttonLabel, buttonLink }
--
-- Everything currently on the live pages is seeded here, so switching the
-- components over to the API loses nothing. Seeding is per page and only when that
-- page has no sections, so a re-run cannot duplicate content someone has edited.
--
-- Page.MetaTitle and MetaDescription have existed unread since 009. They are
-- populated here and the pages will finally use them.
-- ---------------------------------------------------------------------------

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PageSections' AND COLUMN_NAME = 'Content');
SET @sql := IF(@c = 0,
  'ALTER TABLE `PageSections` ADD COLUMN `Content` LONGTEXT NULL AFTER `Title`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ---------------------------------------------------------------- the four pages
INSERT INTO `Pages` (`TenantId`, `Title`, `Slug`, `Type`, `IsPublished`, `MetaTitle`, `MetaDescription`, `CreatedAt`)
SELECT * FROM (
  SELECT 1 AS TenantId, 'About us' AS Title, 'about' AS Slug, 'Custom' AS Type, 1 AS IsPublished,
         'About Us' AS MetaTitle,
         'Who we are and how we print calendars.' AS MetaDescription, NOW() AS CreatedAt
) AS p
WHERE NOT EXISTS (SELECT 1 FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'about');

INSERT INTO `Pages` (`TenantId`, `Title`, `Slug`, `Type`, `IsPublished`, `MetaTitle`, `MetaDescription`, `CreatedAt`)
SELECT * FROM (
  SELECT 1 AS TenantId, 'Frequently asked questions' AS Title, 'faq' AS Slug, 'Custom' AS Type,
         1 AS IsPublished, 'FAQ' AS MetaTitle,
         'Answers to common questions about ordering, delivery and bulk pricing.' AS MetaDescription,
         NOW() AS CreatedAt
) AS p
WHERE NOT EXISTS (SELECT 1 FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'faq');

INSERT INTO `Pages` (`TenantId`, `Title`, `Slug`, `Type`, `IsPublished`, `MetaTitle`, `MetaDescription`, `CreatedAt`)
SELECT * FROM (
  SELECT 1 AS TenantId, 'Buying guide' AS Title, 'buying-guide' AS Slug, 'Custom' AS Type,
         1 AS IsPublished, 'Buying Guide' AS MetaTitle,
         'How to choose the right calendar: order type, size, paper and finish.' AS MetaDescription,
         NOW() AS CreatedAt
) AS p
WHERE NOT EXISTS (SELECT 1 FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'buying-guide');

INSERT INTO `Pages` (`TenantId`, `Title`, `Slug`, `Type`, `IsPublished`, `MetaTitle`, `MetaDescription`, `CreatedAt`)
SELECT * FROM (
  SELECT 1 AS TenantId, 'Contact us' AS Title, 'contact' AS Slug, 'Custom' AS Type,
         1 AS IsPublished, 'Contact Us' AS MetaTitle,
         'Get in touch about orders, customization or bulk enquiries.' AS MetaDescription,
         NOW() AS CreatedAt
) AS p
WHERE NOT EXISTS (SELECT 1 FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'contact');

-- ---------------------------------------------------------------- About
SET @pid := (SELECT `PageId` FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'about' LIMIT 1);
SET @empty := (SELECT COUNT(*) = 0 FROM `PageSections` WHERE `PageId` = @pid);

INSERT INTO `PageSections` (`PageId`, `SectionType`, `Title`, `Content`, `DisplayOrder`, `IsVisible`, `CreatedAt`)
SELECT * FROM (
  SELECT @pid AS PageId, 'Prose' AS SectionType, 'About CalendarShop' AS Title,
    CONCAT('<p>We help businesses and individuals turn the year into something personal ',
           '&mdash; premium, fully customizable calendars printed with your photos, brand name and logo.</p>',
           '<p>From a single wall calendar to bulk corporate gifting, we obsess over print quality, ',
           'on-time delivery and a buying experience that is genuinely simple.</p>') AS Content,
    1 AS DisplayOrder, 1 AS IsVisible, NOW() AS CreatedAt
  UNION ALL SELECT @pid, 'Stats', NULL,
    '[{"value":"10,000+","label":"Calendars printed"},{"value":"1,000+","label":"Happy customers"},{"value":"4.5\\u2605","label":"Average rating"},{"value":"Pan-India","label":"Delivery"}]',
    2, 1, NOW()
  UNION ALL SELECT @pid, 'Cards', 'What we stand for',
    '[{"icon":"\\ud83d\\udda8\\ufe0f","title":"Premium print quality","text":"Full-colour HD printing on heavy art paper."},{"icon":"\\u23f1\\ufe0f","title":"On-time delivery","text":"We ship on schedule, every time."},{"icon":"\\ud83c\\udfa8","title":"Easy customization","text":"Your photos, text, brand name and logo."},{"icon":"\\ud83d\\udce6","title":"Bulk-friendly","text":"Great pricing for corporate gifting at scale."}]',
    3, 1, NOW()
  UNION ALL SELECT @pid, 'Cta', NULL,
    '{"heading":"Ready to design your calendar?","text":"Pick a style, add your photos, and we will handle the rest.","buttonLabel":"Shop calendars","buttonLink":"/order"}',
    4, 1, NOW()
) AS seed
WHERE @empty;

-- ---------------------------------------------------------------- FAQ
SET @pid := (SELECT `PageId` FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'faq' LIMIT 1);
SET @empty := (SELECT COUNT(*) = 0 FROM `PageSections` WHERE `PageId` = @pid);

INSERT INTO `PageSections` (`PageId`, `SectionType`, `Title`, `Content`, `DisplayOrder`, `IsVisible`, `CreatedAt`)
SELECT * FROM (
  SELECT @pid AS PageId, 'Faq' AS SectionType, 'How do I customize my calendar?' AS Title,
    'Open any product and choose "Upload design" to add your own artwork, or pick a template and add your photos, text, brand name and logo.' AS Content,
    1 AS DisplayOrder, 1 AS IsVisible, NOW() AS CreatedAt
  UNION ALL SELECT @pid, 'Faq', 'What sizes and finishes are available?',
    'It depends on the product. Most wall calendars come in A4 and A3 with glossy or matte finishes. The exact options are listed on each product page.', 2, 1, NOW()
  UNION ALL SELECT @pid, 'Faq', 'Do you offer bulk / corporate pricing?',
    'Yes. We specialise in bulk corporate gifting with tiered pricing. Use the Contact page or call us for a quote.', 3, 1, NOW()
  UNION ALL SELECT @pid, 'Faq', 'How long does delivery take?',
    'Standard orders are printed and shipped within a few business days, with pan-India delivery. Same-day delivery is available in select cities.', 4, 1, NOW()
  UNION ALL SELECT @pid, 'Faq', 'What are the shipping charges?',
    'Shipping is calculated at checkout based on your pincode, and many products ship free above a threshold.', 5, 1, NOW()
  UNION ALL SELECT @pid, 'Faq', 'Can I return or replace a calendar?',
    'Personalized products are made to order, but if your item arrives damaged or defective we will replace it. Just reach out within 7 days.', 6, 1, NOW()
  UNION ALL SELECT @pid, 'Faq', 'What payment methods do you accept?',
    'We accept all major UPI apps, cards, net-banking and wallets via our secure payment gateway.', 7, 1, NOW()
  UNION ALL SELECT @pid, 'Faq', 'Can I see a proof before printing?',
    'For custom and bulk orders, our team can share a digital proof for approval before we go to print.', 8, 1, NOW()
) AS seed
WHERE @empty;

-- ---------------------------------------------------------------- Buying guide
-- Seeded as the skeleton from the reference layout, with the shop's own wording to
-- be written in admin. Empty headings would be worse than none, so each step opens
-- with a line saying what belongs there.
SET @pid := (SELECT `PageId` FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'buying-guide' LIMIT 1);
SET @empty := (SELECT COUNT(*) = 0 FROM `PageSections` WHERE `PageId` = @pid);

INSERT INTO `PageSections` (`PageId`, `SectionType`, `Title`, `Content`, `DisplayOrder`, `IsVisible`, `CreatedAt`)
SELECT * FROM (
  SELECT @pid AS PageId, 'Prose' AS SectionType, 'Step 1: Choose your order category' AS Title,
    CONCAT('<p>Decide your order type based on your business or personal needs.</p><ul>',
           '<li><strong>Finished calendars (ready to hang):</strong> fully bound with mounting, ',
           'custom headers if requested, and hangers. For retail sale, gifts or corporate distribution.</li>',
           '<li><strong>Loose sheets (bulk wholesale):</strong> unbound monthly sheet sets printed in ',
           'large production lots, for assembly houses and distributors handling larger runs.</li></ul>') AS Content,
    1 AS DisplayOrder, 1 AS IsVisible, NOW() AS CreatedAt
  UNION ALL SELECT @pid, 'Prose', 'Step 2: Select your size',
    '<p>We print a range of standard and grand commercial sizes to suit any wall or branding requirement. List your sizes here.</p>', 2, 1, NOW()
  UNION ALL SELECT @pid, 'Prose', 'Step 3: Choose paper quality and thickness',
    '<p>Describe the paper stocks you offer and what each is best suited to.</p>', 3, 1, NOW()
  UNION ALL SELECT @pid, 'Prose', 'Step 4: Pick your colour theme and layout',
    '<p>Describe the print layouts and language formats you offer.</p>', 4, 1, NOW()
  UNION ALL SELECT @pid, 'Prose', 'Step 5: Finalize and place your order',
    CONCAT('<p>Once you have identified your specifications, ordering is straightforward.</p><ol>',
           '<li><strong>Build an estimate:</strong> set quantities against the price list and place the order online.</li>',
           '<li><strong>Request a bulk quote:</strong> for custom branding or large loose-sheet lots, ',
           'send us your requirements and we will price it.</li>',
           '<li><strong>Delivery:</strong> every order is packed and dispatched to arrive before your date.</li></ol>'),
    5, 1, NOW()
  UNION ALL SELECT @pid, 'Prose', 'Need assistance?',
    '<p>If you have questions about custom plates, branding formats or transport, get in touch and we will help you set up for the year ahead.</p>', 6, 1, NOW()
) AS seed
WHERE @empty;

-- ---------------------------------------------------------------- Contact
-- Only the wording. The form is code -- it feeds the contacts inbox -- and the
-- address, phone, email and hours are settings below, so the footer and the
-- storefront's structured data can read the same values.
SET @pid := (SELECT `PageId` FROM `Pages` WHERE `TenantId` = 1 AND `Slug` = 'contact' LIMIT 1);
SET @empty := (SELECT COUNT(*) = 0 FROM `PageSections` WHERE `PageId` = @pid);

INSERT INTO `PageSections` (`PageId`, `SectionType`, `Title`, `Content`, `DisplayOrder`, `IsVisible`, `CreatedAt`)
SELECT * FROM (
  SELECT @pid AS PageId, 'Prose' AS SectionType, 'Contact us' AS Title,
    '<p>Questions about an order, customization or bulk pricing? Send us a message and we will get back to you.</p>' AS Content,
    1 AS DisplayOrder, 1 AS IsVisible, NOW() AS CreatedAt
) AS seed
WHERE @empty;

-- ------------------------------------------------- contact details, as settings
-- Deliberately blank rather than seeded with the placeholders that are on the site
-- now (support@calendarshop.example, a Chennai address while the structured data
-- says Madurai). A blank field asks to be filled in; a plausible wrong one does not.
INSERT INTO `Settings` (`TenantId`, `SettingKey`, `SettingValue`, `Category`, `Description`, `CreatedAt`)
SELECT * FROM (
  SELECT 1 AS TenantId, 'Store.AddressLine' AS SettingKey, '' AS SettingValue, 'Contact' AS Category,
         'Postal address shown on the contact page and in structured data.' AS Description, NOW() AS CreatedAt
  UNION ALL SELECT 1, 'Store.Phone', '', 'Contact', 'Public phone number.', NOW()
  UNION ALL SELECT 1, 'Store.Email', '', 'Contact', 'Public email address.', NOW()
  UNION ALL SELECT 1, 'Store.Hours', '', 'Contact', 'Opening hours, e.g. Mon-Sat, 9:30 AM - 6:30 PM.', NOW()
  UNION ALL SELECT 1, 'Store.City', '', 'Contact', 'City, used by the storefront structured data.', NOW()
) AS s
WHERE NOT EXISTS (SELECT 1 FROM `Settings` WHERE `TenantId` = 1 AND `SettingKey` = 'Store.AddressLine');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '055_content_pages.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '055_content_pages.sql');
