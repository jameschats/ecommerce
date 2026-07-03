# AI-0 — Foundation

**Goal:** stand up the `ecomm.ai` service with a provider-agnostic generation core and a bullet-proof credit system — the plumbing every later stage rides on. No merchant-facing features yet beyond one text provider to prove the loop.

## Scope & checklist
- [ ] **New project `ecomm.ai`** — sibling of `ecomm.api`, same conventions (vertical slices under `Features/`, `ApiResponse<T>`, Serilog, `AppException`).
- [ ] **Provider abstraction** — `ITextAIProvider` / `IImageAIProvider` / `IVideoAIProvider` interfaces + an `IAiProviderFactory` that resolves `(capability, useCase, planTier)` from config (`Ai:Text:Provider`, …). Same pattern as V1's `IPaymentProvider`.
- [ ] **First text provider** — `GeminiTextProvider` (Flash — cheapest for volume). Register 1 provider now; others are config-added later.
- [ ] **Provider failover** — on error, fall back to the next-cheapest working provider; log the substitution.
- [ ] **Credit system** — `AiCreditLedger` (append-only), `AiSubscriptions`, `AiUsageLogs`. **Debit before generate, refund on failure, idempotent on `GeneratedContentId`.** Ledger is source of truth; `CreditsUsedThisPeriod` is a reconciled cache. See [design-v3.md §5](../design-v3.md).
- [ ] **Cost tracking** — every call writes `Provider`, `ModelUsed`, token counts, `ProviderCostInPaise`, `DurationMs` to `AiUsageLogs`.
- [ ] **Account identity + bridge** — AI-engine account GUID; a mapping table links it to a commerce `bigint TenantId` when a customer uses both products (keep identity spaces separate — [design-v3.md §2](../design-v3.md)).
- [ ] **AI plan management in Super Admin** — plans, credits, top-up packs (reuses the V2-3 super-admin app).

## Data model (`ai_engine`)
Migrations `ai-001+`: `AiSubscriptions`, `AiCreditLedger`, `AiUsageLogs`, `AiGeneratedContent`, `AiPromptTemplates`, plus the account↔tenant mapping table. `TenantId` here is the AI-engine account GUID (`CHAR(36)`) — **not** the commerce bigint (§2).

## Gate
A single text generation: reserves credits → calls Gemini Flash → writes `AiGeneratedContent` + `AiUsageLogs` (with real paise cost) → debits the ledger; a forced provider error refunds the credits; a duplicate submit (same `GeneratedContentId`) does not double-charge; failover to a second provider works when the primary is down.

## Dependencies
Commerce Platform **V2 stable** (for the own-platform bridge; the engine itself can run standalone). Hangfire (shared with V2) for async jobs later.

**Status:** ⬜ Not started. **Foundation for all AI stages.**
