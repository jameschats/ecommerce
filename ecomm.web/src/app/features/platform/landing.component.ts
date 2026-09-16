import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PlanOption } from '../../core/models/onboarding.model';
import { AuthService } from '../../core/services/auth.service';
import { OnboardingService } from '../../core/services/onboarding.service';
import { PlatformBrandService } from '../../core/services/platform-brand.service';
import { SeoService } from '../../core/services/seo.service';

/**
 * The platform apex landing (wavcommerce.online) — a SaaS marketing + pricing page, not a storefront.
 * Self-contained chrome (own header/footer); the app shell suppresses storefront chrome on /welcome.
 * Every CTA leads into the free trial signup.
 *
 * Visual identity is fixed (not Theme-Engine driven, unlike tenant storefronts) — brand fonts come
 * from PlatformBrandService, shared with the login/signup pages' own platform header.
 */
@Component({
  selector: 'app-landing',
  imports: [RouterLink, CurrencyPipe],
  styles: [`
    :host {
      --display: 'Sora', ui-sans-serif, system-ui, sans-serif;
      --body: 'Manrope', ui-sans-serif, system-ui, sans-serif;
      --mono: 'JetBrains Mono', ui-monospace, SFMono-Regular, monospace;
      font-family: var(--body);
    }
    .f-display { font-family: var(--display); letter-spacing: -0.02em; }
    .f-mono { font-family: var(--mono); font-variant-numeric: tabular-nums; }
    .dotgrid {
      background-image: radial-gradient(circle, rgba(30, 41, 59, 0.09) 1px, transparent 1.4px);
      background-size: 22px 22px;
    }
    .dotgrid-dark {
      background-image: radial-gradient(circle, rgba(255, 255, 255, 0.08) 1px, transparent 1.4px);
      background-size: 22px 22px;
    }
    .check-dot {
      flex: none; width: 17px; height: 17px; border-radius: 9999px;
      background: rgb(238 242 255); color: rgb(79 70 229);
      display: inline-flex; align-items: center; justify-content: center;
      font-size: 10px; font-weight: 700;
    }
    .steps-rule::before {
      content: "";
      position: absolute;
      top: 21px; left: 8%; right: 8%; height: 1px;
      background-image: linear-gradient(90deg, rgb(226 232 240) 50%, transparent 50%);
      background-size: 10px 1px;
    }
    @media (max-width: 760px) { .steps-rule::before { display: none; } }

    /* Nav links: an underline that grows from the center on hover/focus, and a quick press-down
       on click — so jumping to a section feels like it responded, not just teleported. */
    .navlink { position: relative; padding-block: 4px; }
    .navlink::after {
      content: "";
      position: absolute; left: 50%; right: 50%; bottom: 0; height: 2px;
      background: rgb(79 70 229);
      border-radius: 2px;
      transition: left 0.22s ease, right 0.22s ease;
    }
    .navlink:hover::after, .navlink:focus-visible::after { left: 0; right: 0; }
    .navlink:active { transform: scale(0.95); }
    @media (prefers-reduced-motion: no-preference) { .navlink { transition: transform 0.1s ease; } }

    /* The section a nav link jumps to gets a brief highlight once it settles into view, so a
       same-page "navigation" reads as an actual event rather than an invisible scroll. */
    .anchor-target { scroll-margin-top: 84px; }
    @media (prefers-reduced-motion: no-preference) {
      .anchor-target:target { animation: anchor-pulse 1.1s ease-out; }
    }
    @keyframes anchor-pulse {
      0% { background-color: rgba(79, 70, 229, 0.08); }
      100% { background-color: transparent; }
    }
  `],
  template: `
    <div class="min-h-screen flex flex-col bg-white text-slate-900">
      <!-- Marketing header -->
      <header class="sticky top-0 z-30 border-b border-slate-200 bg-white/85 backdrop-blur">
        <div class="max-w-6xl mx-auto px-5 h-[68px] flex items-center justify-between">
          <a routerLink="/welcome" class="f-display font-extrabold text-xl">Wav<span class="text-indigo-600">Commerce</span></a>
          <nav class="hidden sm:flex items-center gap-8 text-sm font-semibold text-slate-600">
            <a routerLink="/welcome" fragment="features" class="navlink hover:text-slate-900">Features</a>
            <a routerLink="/welcome" fragment="pricing" class="navlink hover:text-slate-900">Pricing</a>
            @if (!auth.isAuthenticated()) { <a routerLink="/login" class="navlink hover:text-slate-900">Log in</a> }
          </nav>
          @if (auth.isSuperAdmin()) {
            <a routerLink="/superadmin" class="bg-slate-900 hover:bg-slate-800 text-white text-sm font-bold px-4 py-2.5 rounded-xl">Platform admin →</a>
          } @else if (auth.isAdmin()) {
            <a routerLink="/admin" class="bg-slate-900 hover:bg-slate-800 text-white text-sm font-bold px-4 py-2.5 rounded-xl">My store admin →</a>
          } @else {
            <a routerLink="/signup" class="bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-bold px-4 py-2.5 rounded-xl">Start free trial</a>
          }
        </div>
      </header>

      <!-- Hero -->
      <section class="dotgrid relative overflow-clip">
        <div class="max-w-6xl mx-auto px-5 pt-16 pb-20 grid lg:grid-cols-[1.05fr_0.95fr] gap-12 items-center">
          <div>
            <span class="inline-flex items-center gap-2 text-xs font-bold text-indigo-700 bg-indigo-50 rounded-full pl-2.5 pr-3 py-1.5">
              <span>🇮🇳</span>14-day free trial · no credit card
            </span>
            <h1 class="f-display text-4xl sm:text-5xl font-extrabold leading-[1.06] mt-5">
              Launch your online store in
              <span class="relative whitespace-nowrap">
                <svg viewBox="0 0 100 12" preserveAspectRatio="none" class="absolute -left-[2%] -bottom-1 w-[104%] h-[0.4em] -z-10"><path d="M0,9 Q50,-3 100,9 L100,12 L0,12 Z" fill="#e0e7ff"/></svg>
                minutes
              </span>
            </h1>
            <p class="text-lg text-slate-600 leading-relaxed mt-5 max-w-[46ch]">Themes, payments, shipping and your own domain — with AI that builds your first catalog and drafts your marketing while you're still picking a name. Start free, upgrade when you're ready.</p>
            <div class="mt-7 flex items-center gap-3 flex-wrap">
              <a routerLink="/signup" class="bg-indigo-600 hover:bg-indigo-700 text-white font-bold px-6 py-3.5 rounded-xl">Start your free trial</a>
              <a routerLink="/welcome" fragment="pricing" class="border border-slate-300 hover:bg-slate-50 text-slate-700 font-bold px-6 py-3.5 rounded-xl">See pricing</a>
            </div>
            <p class="text-xs text-slate-400 mt-3">Free for 14 days · No card required · Cancel anytime</p>
          </div>

          <div class="relative">
            <div class="absolute -top-3.5 right-[8%] z-[3] rotate-[-3deg] flex items-center gap-2 bg-slate-900 text-indigo-50 rounded-xl px-3.5 py-2.5 text-xs font-bold shadow-lg shadow-slate-900/30">
              <span class="w-2 h-2 rounded-full bg-amber-400"></span>✨ AI-built catalog in 2 min
            </div>
            <div class="relative z-[2] rotate-[2.2deg] bg-white border border-slate-200 rounded-2xl overflow-hidden shadow-[0_30px_60px_-20px_rgba(30,27,75,0.28)]">
              <div class="flex items-center gap-1.5 px-3.5 py-2.5 border-b border-slate-200 bg-slate-50">
                <i class="w-2 h-2 rounded-full bg-slate-300"></i><i class="w-2 h-2 rounded-full bg-slate-300"></i><i class="w-2 h-2 rounded-full bg-slate-300"></i>
                <span class="f-mono ml-2 text-[0.68rem] text-slate-400 bg-white border border-slate-200 rounded-md px-2.5 py-1 flex-1">yourstore.wavcommerce.online</span>
              </div>
              <div class="p-4 grid gap-2.5">
                <div class="h-[60px] rounded-lg bg-gradient-to-r from-indigo-50 to-slate-50 flex items-center px-4 gap-2.5">
                  <span class="text-[0.62rem] font-bold text-indigo-600 bg-white rounded-full px-2.5 py-1">New arrivals</span>
                  <span class="text-[0.62rem] font-bold text-indigo-600/60 bg-white rounded-full px-2.5 py-1">Shop now</span>
                </div>
                <div class="grid grid-cols-3 gap-2">
                  @for (i of [1,2,3]; track i) {
                    <div class="border border-slate-200 rounded-lg p-2">
                      <div class="h-[42px] rounded-md bg-slate-50 mb-1.5"></div>
                      <div class="h-1.5 rounded-full bg-slate-200 mb-1"></div>
                      <div class="h-1.5 rounded-full bg-slate-200 w-3/5"></div>
                    </div>
                  }
                </div>
              </div>
            </div>
            <div class="absolute -bottom-4 left-[4%] z-[3] flex items-center gap-2 bg-white border border-slate-200 rounded-xl px-3.5 py-2.5 text-xs font-bold text-slate-800 shadow-lg shadow-slate-900/10 rotate-[2deg]">💳 UPI · COD · Cards</div>
          </div>
        </div>
      </section>

      <!-- India + AI band -->
      <section class="bg-gradient-to-br from-slate-950 to-indigo-950 text-indigo-50">
        <div class="max-w-6xl mx-auto px-5 py-14">
          <div class="grid md:grid-cols-2 rounded-[20px] overflow-hidden border border-white/10">
            <div class="p-8 sm:p-9">
              <p class="text-xs font-bold tracking-wider uppercase text-indigo-300/70">Built for India</p>
              <h3 class="f-display text-xl sm:text-2xl font-bold text-white mt-2.5">Payments &amp; tax that already know the rules</h3>
              <p class="text-sm text-indigo-200/70 leading-relaxed mt-2.5 max-w-[42ch]">UPI and Cash on Delivery out of the box, GST-ready invoicing, and pincode-level shipping zones — the defaults are set for how India actually buys.</p>
              <div class="flex flex-wrap gap-2 mt-4">
                <span class="text-xs font-bold text-indigo-200 border border-white/15 rounded-full px-2.5 py-1">UPI &amp; COD</span>
                <span class="text-xs font-bold text-indigo-200 border border-white/15 rounded-full px-2.5 py-1">GST invoicing</span>
                <span class="text-xs font-bold text-indigo-200 border border-white/15 rounded-full px-2.5 py-1">WhatsApp order updates</span>
              </div>
            </div>
            <div class="p-8 sm:p-9 border-t md:border-t-0 md:border-l border-white/10">
              <p class="text-xs font-bold tracking-wider uppercase text-amber-400/80">AI does the busywork</p>
              <h3 class="f-display text-xl sm:text-2xl font-bold text-white mt-2.5">Skip the blank-page problem entirely</h3>
              <p class="text-sm text-indigo-200/70 leading-relaxed mt-2.5 max-w-[42ch]">Generate a full sample catalog from your store type, import from Shopify or a spreadsheet in one pass, and let the Marketing Studio draft your first month of posts.</p>
              <div class="flex flex-wrap gap-2 mt-4">
                <span class="text-xs font-bold text-amber-400 border border-white/15 rounded-full px-2.5 py-1">AI catalog builder</span>
                <span class="text-xs font-bold text-amber-400 border border-white/15 rounded-full px-2.5 py-1">AI Marketing Studio</span>
                <span class="text-xs font-bold text-amber-400 border border-white/15 rounded-full px-2.5 py-1">One-click import</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      <!-- Features (bento) -->
      <section id="features" class="anchor-target py-16">
        <div class="max-w-6xl mx-auto px-5">
          <div class="max-w-xl mb-10">
            <p class="text-xs font-bold uppercase tracking-wider text-indigo-600">Everything included</p>
            <h2 class="f-display text-2xl sm:text-3xl font-bold mt-2">One platform, not a stack of plugins</h2>
            <p class="text-slate-600 mt-2.5">Every store starts with the same core — no add-on marketplace to assemble before you can sell.</p>
          </div>

          <div class="grid md:grid-cols-3 gap-4">
            <div class="md:row-span-2 bg-gradient-to-br from-indigo-50 to-white border border-indigo-100 rounded-2xl p-7 flex flex-col gap-3 hover:-translate-y-1 hover:shadow-lg hover:shadow-indigo-900/10 transition">
              <span class="self-start text-[0.68rem] font-bold text-amber-600 bg-amber-50 rounded-full px-2.5 py-1">✨ AI-powered</span>
              <div class="w-10 h-10 rounded-xl bg-indigo-600 text-white flex items-center justify-center">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 3v4M12 17v4M3 12h4M17 12h4"/><circle cx="12" cy="12" r="4"/></svg>
              </div>
              <h3 class="f-display font-bold text-lg mt-1">AI-assisted store setup</h3>
              <p class="text-sm text-slate-600 leading-relaxed">Describe your store type and get a full sample catalog with real product photography — every item stays fully editable, or import your existing catalog straight from Shopify, WooCommerce, Wix or a spreadsheet.</p>
            </div>

            @for (f of features; track f.title) {
              <div class="bg-white border border-slate-200 rounded-2xl p-6 flex flex-col gap-2.5 hover:-translate-y-1 hover:shadow-lg hover:shadow-indigo-900/10 transition">
                <div class="w-10 h-10 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center" [innerHTML]="f.icon"></div>
                <h3 class="font-bold mt-1">{{ f.title }}</h3>
                <p class="text-sm text-slate-600 leading-relaxed">{{ f.body }}</p>
              </div>
            }
          </div>
        </div>
      </section>

      <!-- How it works -->
      <section class="bg-slate-50 border-y border-slate-100 py-16">
        <div class="max-w-6xl mx-auto px-5">
          <div class="max-w-xl mb-10">
            <p class="text-xs font-bold uppercase tracking-wider text-indigo-600">Getting started</p>
            <h2 class="f-display text-2xl sm:text-3xl font-bold mt-2">From sign-up to selling, in three steps</h2>
          </div>
          <div class="steps-rule relative grid sm:grid-cols-3 gap-8">
            @for (s of steps; track s.n) {
              <div class="relative">
                <div class="f-mono relative z-[1] w-11 h-11 rounded-full bg-white border-[1.5px] border-indigo-600 text-indigo-600 font-bold flex items-center justify-center mb-4">{{ s.n }}</div>
                <h3 class="font-bold text-[1.05rem] mb-1.5">{{ s.title }}</h3>
                <p class="text-sm text-slate-600 leading-relaxed max-w-[30ch]">{{ s.body }}</p>
              </div>
            }
          </div>
        </div>
      </section>

      <!-- Pricing -->
      <section id="pricing" class="anchor-target py-16">
        <div class="max-w-6xl mx-auto px-5">
          <div class="text-center mb-10">
            <p class="text-xs font-bold uppercase tracking-wider text-indigo-600">Simple, transparent pricing</p>
            <h2 class="f-display text-2xl sm:text-3xl font-bold mt-2">Start free. Grow into a plan that fits.</h2>
            <p class="text-slate-600 mt-2">Start with a <span class="font-semibold text-slate-900">14-day free trial</span> on any plan — no card needed.</p>
          </div>

          @if (plans().length) {
            <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-5 max-w-4xl mx-auto items-start">
              @for (p of plans(); track p.planId; let i = $index) {
                <div class="bg-white border rounded-2xl p-7 flex flex-col"
                     [class]="i === 1 ? 'border-indigo-600 shadow-[0_24px_48px_-22px_rgba(79,70,229,0.35)] lg:-translate-y-2.5' : 'border-slate-200'">
                  @if (i === 1) { <span class="self-start text-xs font-bold text-white bg-indigo-600 rounded-full px-2.5 py-1 mb-3.5">Most popular</span> }
                  <h3 class="font-bold text-lg">{{ p.name }}</h3>
                  @if (p.introPriceInr != null && p.introMonths) {
                    <div class="flex items-baseline gap-2 mt-3.5">
                      <span class="f-mono text-[2.1rem] font-bold">{{ p.introPriceInr | currency:'INR':'symbol':'1.0-0' }}</span><span class="text-slate-400 text-sm">/mo</span>
                    </div>
                    <span class="f-mono text-sm text-slate-400 line-through">{{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}</span>
                    <p class="mt-2 inline-block text-xs font-bold text-green-700 bg-green-50 border border-green-200 rounded-full px-2.5 py-1 self-start">
                      First {{ p.introMonths }} months{{ percentOff(p) ? ' · ' + percentOff(p) + '% off' : '' }}
                    </p>
                    <p class="text-xs text-slate-400 mt-1">Then {{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}/mo</p>
                  } @else {
                    <div class="flex items-baseline gap-2 mt-3.5">
                      <span class="f-mono text-[2.1rem] font-bold">{{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}</span><span class="text-slate-400 text-sm">/mo</span>
                    </div>
                  }
                  <ul class="text-sm text-slate-600 mt-5 space-y-2.5 flex-1">
                    <li class="flex items-center gap-2.5"><span class="check-dot">✓</span>{{ p.maxProducts ? (p.maxProducts + ' products') : 'Unlimited products' }}</li>
                    <li class="flex items-center gap-2.5"><span class="check-dot">✓</span>{{ p.maxOrders ? (p.maxOrders + ' orders / mo') : 'Unlimited orders' }}</li>
                    <li class="flex items-center gap-2.5"><span class="check-dot">✓</span>{{ p.maxStorageMb ? ((p.maxStorageMb / 1024) + ' GB storage') : 'Unlimited storage' }}</li>
                    <li class="flex items-center gap-2.5"><span class="check-dot">✓</span>{{ p.aiCredits }} AI credits</li>
                    <li class="flex items-center gap-2.5"><span class="check-dot">✓</span>Themes, custom domain &amp; more</li>
                    @if (p.marketingEngineLevel) { <li class="flex items-center gap-2.5"><span class="check-dot">✓</span>Marketing engine: {{ p.marketingEngineLevel }}</li> }
                    @if (p.liveChatLevel) { <li class="flex items-center gap-2.5"><span class="check-dot">✓</span>Live chat: {{ p.liveChatLevel }}</li> }
                    @if (p.helpdeskLevel) { <li class="flex items-center gap-2.5"><span class="check-dot">✓</span>Helpdesk: {{ p.helpdeskLevel }}</li> }
                  </ul>
                  <a routerLink="/signup" [queryParams]="{ plan: p.slug }"
                     class="mt-6 text-center font-bold px-4 py-3 rounded-xl"
                     [class]="i === 1 ? 'bg-indigo-600 hover:bg-indigo-700 text-white' : 'border border-slate-300 hover:bg-slate-50 text-slate-700'">Start free trial</a>
                </div>
              }
            </div>
          } @else {
            <p class="text-center text-slate-400">Loading plans…</p>
          }
        </div>
      </section>

      <!-- Trust strip -->
      <section class="bg-slate-50 border-y border-slate-100 py-10">
        <div class="max-w-6xl mx-auto px-5 flex flex-wrap gap-3 justify-center">
          @for (t of trust; track t.label) {
            <span class="flex items-center gap-2 text-sm font-semibold text-slate-600 border border-slate-200 bg-white rounded-full px-4 py-2.5">
              <span class="text-indigo-600" [innerHTML]="t.icon"></span>{{ t.label }}
            </span>
          }
        </div>
      </section>

      <!-- Final CTA -->
      <section class="dotgrid-dark bg-gradient-to-br from-indigo-950 to-slate-950 text-white text-center">
        <div class="max-w-6xl mx-auto px-5 py-20">
          <h2 class="f-display text-3xl sm:text-4xl font-bold max-w-[20ch] mx-auto">Ready to start selling?</h2>
          <p class="text-indigo-200/70 mt-3.5">Set up your store today — free for 14 days, no card required.</p>
          <a routerLink="/signup" class="inline-block mt-7 bg-amber-400 hover:brightness-105 text-slate-900 font-bold px-7 py-3.5 rounded-xl">Start your free trial</a>
        </div>
      </section>

      <!-- Footer -->
      <footer class="mt-auto border-t border-slate-100">
        <div class="max-w-6xl mx-auto px-5 py-8 flex flex-col sm:flex-row items-center justify-between gap-3 text-sm text-slate-500">
          <span class="f-display font-extrabold text-slate-800">Wav<span class="text-indigo-600">Commerce</span></span>
          <div class="flex gap-5">
            <a routerLink="/welcome" fragment="features" class="hover:text-slate-800">Features</a>
            <a routerLink="/welcome" fragment="pricing" class="hover:text-slate-800">Pricing</a>
            <a routerLink="/signup" class="hover:text-slate-800">Start free</a>
            <a routerLink="/login" class="hover:text-slate-800">Log in</a>
          </div>
          <span>© {{ year }} WavCommerce</span>
        </div>
      </footer>
    </div>
  `,
})
export class LandingComponent implements OnInit {
  private readonly onboarding = inject(OnboardingService);
  private readonly seo = inject(SeoService);
  private readonly brand = inject(PlatformBrandService);
  /** Signed-in staff landing on the apex need a way back into their console. */
  readonly auth = inject(AuthService);

  readonly plans = signal<PlanOption[]>([]);
  readonly year = 2026;

  /** Discount implied by the intro price, so "₹20 on a ₹1999 plan" reads as "90% off". */
  percentOff(p: PlanOption): number {
    if (p.introPriceInr == null || p.monthlyPrice <= 0) return 0;
    return Math.round(((p.monthlyPrice - p.introPriceInr) / p.monthlyPrice) * 100);
  }

  private static readonly ICON_THEME = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 4h16v4H4zM4 12h7v8H4zM14 12h6v8h-6z"/></svg>`;
  private static readonly ICON_PAYMENTS = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20"/></svg>`;
  private static readonly ICON_SHIPPING = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 7h11v10H3zM14 10h4l3 3v4h-7z"/><circle cx="7" cy="18" r="1.6"/><circle cx="17.5" cy="18" r="1.6"/></svg>`;
  private static readonly ICON_DOMAIN = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><path d="M2 12h20M12 3a15 15 0 0 1 0 18 15 15 0 0 1 0-18Z"/></svg>`;

  readonly features = [
    { icon: LandingComponent.ICON_THEME, title: 'Beautiful themes', body: 'Install a free theme and customize colors, fonts and layout — no code required.' },
    { icon: LandingComponent.ICON_PAYMENTS, title: 'Payments built in', body: 'Accept cards, UPI and Cash on Delivery out of the box.' },
    { icon: LandingComponent.ICON_SHIPPING, title: 'Shipping & delivery', body: 'Set zones, rates and courier integration by pincode.' },
    { icon: LandingComponent.ICON_DOMAIN, title: 'Your own domain', body: 'Go live on your brand — connect a custom domain in minutes.' },
  ];

  readonly steps = [
    { n: '01', title: 'Create your account', body: 'No credit card. Your store gets its own free subdomain instantly.' },
    { n: '02', title: 'Pick a theme, or let AI build it', body: 'Install a free theme, or generate a starter catalog and layout in one go.' },
    { n: '03', title: 'Connect your domain and go live', body: 'Payments and shipping are already switched on — just start taking orders.' },
  ];

  readonly trust = [
    { icon: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 12l2 2 4-4"/><circle cx="12" cy="12" r="9"/></svg>`, label: 'GST-ready invoicing' },
    { icon: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20"/></svg>`, label: 'UPI & Cash on Delivery' },
    { icon: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 11.5a8.4 8.4 0 0 1-8.6 8.4A8.5 8.5 0 0 1 8 18.6L3 20l1.4-4.8A8.4 8.4 0 1 1 21 11.5Z"/></svg>`, label: 'WhatsApp order updates' },
    { icon: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2l8 4v6c0 5-3.4 8.4-8 10-4.6-1.6-8-5-8-10V6l8-4Z"/></svg>`, label: '99.9% uptime' },
  ];

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'WavCommerce — Launch your online store in minutes',
      description: 'Build, run and grow your online store with WavCommerce. AI-assisted setup, themes, UPI/COD payments, shipping and your own domain. Start free — 14-day trial, no card required.',
    });
    this.onboarding.plans().subscribe((p) => this.plans.set(p));
    this.brand.loadFonts();
  }
}
