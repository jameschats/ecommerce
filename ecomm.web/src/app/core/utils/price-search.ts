/** Round price ceilings a shopper would actually recognise, generic across any store vertical (a
 *  boutique's ₹500–3,000 shirts and an electronics store's ₹15,000–80,000 phones both get sensible,
 *  differently-scaled options from the same list). Picks up to 2 spread across the given [min, max]
 *  range — not clustered together — rather than every step that happens to fall inside it.
 *  Shared by the header search and the on-page collection search — same suggestions everywhere. */
const PRICE_STEPS = [500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000, 500000, 1000000];
export function priceBreakpoints(min: number, max: number): number[] {
  if (!(max > min) || max <= 0) return [];
  const candidates = PRICE_STEPS.filter((p) => p > min && p < max);
  if (!candidates.length) return [];
  const picks = [candidates[Math.floor(candidates.length / 3)], candidates[Math.floor((candidates.length * 2) / 3)]];
  return Array.from(new Set(picks));
}

/** Parses a trailing "under/below/less than ₹N" phrase off free-typed search text (any currency
 *  prefix, comma-formatted numbers) so typing "mobiles under 20000" and hitting enter works even
 *  without picking a suggestion chip. Generic — no vertical/category-specific parsing. */
const PRICE_CEILING_RE = /\s*(?:under|below|less than)\s*(?:₹|rs\.?|inr)?\s*([\d,]+)\s*$/i;
export function parsePriceCeiling(text: string): { term: string; maxPrice: number } | null {
  const m = PRICE_CEILING_RE.exec(text);
  if (!m) return null;
  const maxPrice = Number(m[1].replace(/,/g, ''));
  if (!Number.isFinite(maxPrice) || maxPrice <= 0) return null;
  return { term: text.slice(0, m.index).trim(), maxPrice };
}
