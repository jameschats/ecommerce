# AI-5 — Video Generation

**Goal:** short marketing videos — reels, slideshows, festival greetings — the highest-value output. **Also the biggest margin risk: build only behind hard credit gates.**

> ⚠️ **Video is the margin killer.** 20 reels ≈ 2,000 credits ≈ ₹1,000–₹2,500 against a ₹1,999 plan. **Maximum 50 videos/month on the highest plan — no exceptions. No plan ever includes unlimited video.** See [design-v3.md §4, §9](../design-v3.md).

## Scope & checklist — content types (with credit costs)
- [ ] **Product Reel 15s** (100) — images + AI voiceover + music + captions
- [ ] **Product Reel 30s** (180)
- [ ] **Slideshow Video** (60) — images + text overlays + transitions + music
- [ ] **Festival Video** (80) — animated greeting w/ brand + offer

## Supporting scope
- [ ] **Video providers** — `InVideoAIProvider` (slideshow→video, India-based, affordable, default), `KlingAIProvider` (good quality, lower cost), `RunwayMLProvider` (Gen-3, premium, heavily metered). Config-selected.
- [ ] **Hard credit gates** — enforce per-plan monthly video caps in code *before* the provider call; block + prompt to buy video credit packs at the cap. This is non-negotiable margin protection.
- [ ] **Slideshow pipeline** — assemble product images + captions + music into a video (the affordable default path).
- [ ] **Long-running async** — Hangfire job (video takes minutes); progress + SignalR completion; refund credits on failure.
- [ ] **Video credit packs** — sell extra at ₹100–₹150 per ~10 videos.

## Gate
Each video type renders from sample product images with captions + music; the per-plan monthly cap **blocks** the 51st video on the top plan and offers a top-up; generation runs async with completion notification; a failed render refunds credits; provider cost is logged to `AiUsageLogs`.

## Dependencies
AI-0 (credits/gates/async), AI-3 (image assets), AI-2 (product images). InVideo/Kling/Runway keys. **Do not start until the credit-cap enforcement from AI-0 is proven.**

**Status:** ⬜ Not started. **Gated on credit-cap enforcement.**
