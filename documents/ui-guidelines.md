# UI / UX Guidelines

**Reference:** a Flipkart-class e-commerce experience — but **simpler, cleaner, more elegant**. Optimize for clarity and ease, not feature density.

## Principles
- **Simple & nice UX:** short paths, obvious next action, minimal clicks to cart/checkout.
- **Elegant look:** generous white space, calm palette, one strong accent color, restrained shadows, rounded corners, crisp typography.
- **Fast & responsive:** mobile-first, skeleton loaders, optimistic UI where safe.
- **Consistent:** all colors/fonts/buttons come from the **Theme Engine** (no hardcoded brand values) so the storefront is admin-themable.
- **Accessible:** semantic HTML, keyboard nav, visible focus, sufficient contrast, alt text.

## Visual language (defaults; overridable by Theme Engine)
- Background: white / very light gray (`#f8fafc`); surfaces white with subtle border.
- Primary accent: `#2563eb` (blue), secondary `#1e293b` (slate). Success/green for price/in-stock, amber for ratings.
- Font: Inter (or system stack). Clear hierarchy: bold product titles, muted secondary text.
- Tailwind utilities; small set of reusable components; rounded-`xl`, soft `shadow-sm`.

## Key storefront screens (Flipkart-style, simplified)
- **Header:** logo, prominent search bar (center), account + cart (right); slim category bar / mega-menu below.
- **Home:** hero banner carousel, category tiles, featured/new/best-seller product rails (driven by CMS sections later).
- **Listing (PLP):** left filters (category, brand, price, attributes, rating), sort dropdown, responsive product grid, pagination/infinite scroll.
- **Product (PDP):** image gallery (left), title/price/variants/CTA (right), attributes table, reviews, related products. Clear **Add to Cart** / **Buy Now**.
- **Cart:** line items with qty steppers, price summary (subtotal, discount, tax, shipping, total), coupon field, checkout CTA. Drawer or page.
- **Checkout:** stepped — address → shipping → payment → review. Show running order summary. Razorpay for payment.
- **Account:** orders + tracking, addresses, profile, downloadable invoices.
- **Auth:** login page renders dynamically from `/api/auth/config` (email/password, mobile OTP, Google) — only enabled methods shown.

## Admin
- Clean dashboard shell (sidebar nav + topbar), data tables with search/sort/paginate, simple forms with inline validation, toasts for feedback. Function over flash.

## Component conventions
- Standalone Angular components, Tailwind for styling, a shared UI kit (button, input, card, badge, modal, toast, table, pagination).
- All API calls return the `ApiResponse<T>` envelope; a typed HTTP layer + interceptor (JWT, error toasts) handles it centrally.

> Treat this as the north star for every screen we build, starting with the Stage-1 auth UI.
