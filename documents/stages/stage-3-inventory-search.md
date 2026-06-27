# Stage 3 — Inventory & Search

**Goal:** stock tracking with reservations, and product search.

## Scope & checklist
### Inventory (✅ done)
- [x] Available vs reserved stock per product/variant (`InventoryService`)
- [x] Inventory transaction ledger (Adjustment/Reservation/Release/Sale/…) with balance-after
- [x] Low-stock alerts (`AvailableQty <= ReorderLevel`) — admin low-stock view + row highlight
- [x] **Reserve / Release / Commit** hooks ready for cart→order (Stage 4/5)
- [x] Set/adjust stock (records transactions) + admin **Inventory UI**
- [x] **Excel import/export** of stock levels (upsert by SKU) + template
- [x] Seeded stock for the calendar catalog (incl. low-stock + out-of-stock demo)

### Search (✅ done)
- [x] Product search via **MySQL FULLTEXT** (Pomelo `EF.Functions.Match`, boolean + prefix) with SKU/LIKE fallback
- [x] Filters + sorting (existing browse) retained
- [x] **Search logging** (`SearchLogs`) + **popular searches** (`PopularSearches`)
- [x] **Autocomplete** suggestions endpoint + header search dropdown (debounced)

### Storefront (✅ done)
- [x] Stock status on PDP (In stock / Only N left / Out of stock) + add-to-cart gating
- [x] Out-of-stock badge on product cards; `inStock` on list/detail DTOs

## Tables (used)
`Inventory, InventoryTransactions, SearchLogs, PopularSearches`

## Notes
- Elasticsearch is a later phase; V1 uses MySQL FULLTEXT.
- Reserve/Release/Commit are implemented as service hooks — wired into checkout in Stage 5.

### Follow-ups closed
- [x] **Attribute search** — product browse now also matches attribute values + category/brand names.
- [x] **Variant-level stock** — `SetVariantStock`/variant inventory endpoints + admin UI variant editor (`HasVariants` flag drives the expander).
- [x] **Transaction history** — admin inventory "History" modal (`GET …/transactions`).
- [x] Fixed a latent **inventory-list 500** (projection rewritten to scalar subqueries, paginated on the entity).

**Status:** ✅ Done — inventory (levels, ledger, low-stock, import/export, admin UI) + FULLTEXT search (logging, popular, autocomplete) + storefront stock display. Verified backend + SSR.
