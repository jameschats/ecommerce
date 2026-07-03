# V3 Build Stages — AI Growth Engine

Sequential build plan for the **AI Growth Engine** (see [`../design-v3.md`](../design-v3.md)) — a standalone AI-marketing SaaS (`ecomm.ai`) with its **own database** (`ai_engine`) and its **own account identity** (a GUID, bridged to the commerce `bigint TenantId` by a mapping table). Migrations live in a dedicated `database/ai-migrations/` folder, numbered `ai-001+`.

| Stage | Title | Status |
|---|---|---|
| [AI-0](v3-stage-0-foundation.md) | **Foundation** — provider abstraction, credit system, cost tracking, first text provider | ⬜ Not started |
| [AI-1](v3-stage-1-text-generation.md) | **Text Generation** (ship first — the MVP) | ⬜ |
| [AI-2](v3-stage-2-connectors.md) | Platform Connectors — Own / Shopify / WooCommerce / CSV | ⬜ |
| [AI-3](v3-stage-3-image-generation.md) | Image Generation — posters, banners, bg removal | ⬜ |
| [AI-4](v3-stage-4-campaign-builder.md) | Campaign Builder — multi-channel, scheduling | ⬜ |
| [AI-5](v3-stage-5-video-generation.md) | Video Generation — reels, slideshows *(hard credit gates)* | ⬜ |
| [AI-6](v3-stage-6-india-differentiators.md) | Indian Market Differentiators — festivals, Hinglish, regional languages | ⬜ |

**Legend:** ✅ done · 🟡 in progress · ⬜ not started

## Two hard rules

1. **Build after Commerce Platform V2 is stable.** The own-platform connector (AI-2) depends on V2 tenant infrastructure. See [../v2-stages/](../v2-stages/).
2. **Ship AI-1 (text) and charge for it before building images/video.** Text has 73–90% margins and validates demand cheaply. **Video is the margin killer** — never build it before the credit system (AI-0) can hard-cap it. See [design-v3.md §5, §9](../design-v3.md).

## Why this is a separate product
The engine connects to Shopify/WooCommerce/Magento too — not just your own platform (AI-2 is "the market multiplier"). It reuses V1's architectural patterns (provider abstraction like `IPaymentProvider`, vertical slices, `ApiResponse<T>`) but shares **no** commerce transactional data. Provider-agnostic by design: `ITextAIProvider` / `IImageAIProvider` / `IVideoAIProvider` behind a factory that routes by use-case, cost, and availability.

> Margin discipline is the whole game: every generation debits credits **before** the provider call and refunds on failure; every call logs `ProviderCostInPaise` so margin erosion is visible before it hurts.
