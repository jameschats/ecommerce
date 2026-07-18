import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PlanOption } from '../../core/models/onboarding.model';
import { AuthService } from '../../core/services/auth.service';
import { OnboardingService } from '../../core/services/onboarding.service';
import { SeoService } from '../../core/services/seo.service';

/**
 * The platform apex landing (wavcommerce.online) — a SaaS marketing + pricing page, not a storefront.
 * Self-contained chrome (own header/footer); the app shell suppresses storefront chrome on /welcome.
 * Every CTA leads into the free trial signup.
 */
@Component({
  selector: 'app-landing',
  imports: [RouterLink, CurrencyPipe],
  template: `
    <div class="min-h-screen flex flex-col bg-white text-slate-900">
      <!-- Marketing header -->
      <header class="border-b border-slate-100">
        <div class="max-w-6xl mx-auto px-5 h-16 flex items-center justify-between">
          <a routerLink="/welcome" class="font-extrabold text-xl tracking-tight">Wav<span class="text-indigo-600">Commerce</span></a>
          <nav class="hidden sm:flex items-center gap-7 text-sm text-slate-600">
            <a routerLink="/welcome" fragment="features" class="hover:text-slate-900">Features</a>
            <a routerLink="/welcome" fragment="pricing" class="hover:text-slate-900">Pricing</a>
            @if (!auth.isAuthenticated()) { <a routerLink="/login" class="hover:text-slate-900">Log in</a> }
          </nav>
          @if (auth.isSuperAdmin()) {
            <a routerLink="/superadmin" class="bg-slate-900 hover:bg-slate-800 text-white text-sm font-medium px-4 py-2 rounded-lg">Platform admin →</a>
          } @else if (auth.isAdmin()) {
            <a routerLink="/admin" class="bg-slate-900 hover:bg-slate-800 text-white text-sm font-medium px-4 py-2 rounded-lg">My store admin →</a>
          } @else {
            <a routerLink="/signup" class="bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium px-4 py-2 rounded-lg">Start free trial</a>
          }
        </div>
      </header>

      <!-- Hero -->
      <section class="max-w-6xl mx-auto px-5 pt-16 pb-14 text-center">
        <span class="inline-block text-xs font-semibold text-indigo-700 bg-indigo-50 rounded-full px-3 py-1">14-day free trial · no credit card</span>
        <h1 class="text-4xl sm:text-5xl font-extrabold tracking-tight mt-5 max-w-3xl mx-auto">Launch your online store in minutes</h1>
        <p class="text-lg text-slate-600 mt-4 max-w-2xl mx-auto">Everything you need to sell online — themes, payments, shipping and your own domain. Start free, upgrade when you're ready.</p>
        <div class="mt-8 flex items-center justify-center gap-3">
          <a routerLink="/signup" class="bg-indigo-600 hover:bg-indigo-700 text-white font-medium px-6 py-3 rounded-lg">Start your free trial</a>
          <a routerLink="/welcome" fragment="pricing" class="border border-slate-300 hover:bg-slate-50 text-slate-700 font-medium px-6 py-3 rounded-lg">See pricing</a>
        </div>
        <p class="text-xs text-slate-400 mt-3">Free for 14 days. No card required. Cancel anytime.</p>
      </section>

      <!-- Features -->
      <section id="features" class="bg-slate-50 border-y border-slate-100">
        <div class="max-w-6xl mx-auto px-5 py-16 grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
          @for (f of features; track f.title) {
            <div class="bg-white border border-slate-200 rounded-xl p-6">
              <div class="text-2xl">{{ f.icon }}</div>
              <h3 class="font-semibold text-slate-900 mt-3">{{ f.title }}</h3>
              <p class="text-sm text-slate-600 mt-1">{{ f.body }}</p>
            </div>
          }
        </div>
      </section>

      <!-- Pricing -->
      <section id="pricing" class="max-w-6xl mx-auto px-5 py-16">
        <div class="text-center mb-10">
          <h2 class="text-3xl font-bold">Simple, transparent pricing</h2>
          <p class="text-slate-600 mt-2">Start with a <span class="font-medium text-slate-900">14-day free trial</span> on any plan — no card needed.</p>
        </div>
        @if (plans().length) {
          <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-5 max-w-4xl mx-auto">
            @for (p of plans(); track p.planId; let i = $index) {
              <div class="border rounded-2xl p-6 flex flex-col" [class]="i === 1 ? 'border-indigo-400 ring-1 ring-indigo-200 shadow-sm' : 'border-slate-200'">
                @if (i === 1) { <span class="text-xs font-semibold text-indigo-700 mb-2">Most popular</span> }
                <h3 class="font-semibold text-lg">{{ p.name }}</h3>
                @if (p.introPriceInr != null && p.introMonths) {
                  <p class="mt-2">
                    <span class="text-3xl font-extrabold">{{ p.introPriceInr | currency:'INR':'symbol':'1.0-0' }}</span><span class="text-slate-400 text-sm">/mo</span>
                    <span class="ml-2 text-sm text-slate-400 line-through">{{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}</span>
                  </p>
                  <p class="mt-1 inline-block text-xs font-semibold text-green-700 bg-green-50 border border-green-200 rounded-full px-2 py-0.5">
                    First {{ p.introMonths }} months{{ percentOff(p) ? ' · ' + percentOff(p) + '% off' : '' }}
                  </p>
                  <p class="text-xs text-slate-400 mt-1">Then {{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}/mo</p>
                } @else {
                  <p class="mt-2"><span class="text-3xl font-extrabold">{{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}</span><span class="text-slate-400 text-sm">/mo</span></p>
                }
                <ul class="text-sm text-slate-600 mt-4 space-y-1.5 flex-1">
                  <li>✓ {{ p.maxProducts ? (p.maxProducts + ' products') : 'Unlimited products' }}</li>
                  <li>✓ {{ p.maxOrders ? (p.maxOrders + ' orders / mo') : 'Unlimited orders' }}</li>
                  <li>✓ {{ p.aiCredits }} AI credits</li>
                  <li>✓ Themes, custom domain &amp; more</li>
                </ul>
                <a routerLink="/signup" [queryParams]="{ plan: p.slug }"
                   class="mt-6 text-center font-medium px-4 py-2.5 rounded-lg"
                   [class]="i === 1 ? 'bg-indigo-600 hover:bg-indigo-700 text-white' : 'border border-slate-300 hover:bg-slate-50 text-slate-700'">Start free trial</a>
              </div>
            }
          </div>
        } @else {
          <p class="text-center text-slate-400">Loading plans…</p>
        }
      </section>

      <!-- CTA band -->
      <section class="bg-indigo-600 text-white">
        <div class="max-w-6xl mx-auto px-5 py-14 text-center">
          <h2 class="text-2xl sm:text-3xl font-bold">Ready to start selling?</h2>
          <p class="text-indigo-100 mt-2">Set up your store today — free for 14 days.</p>
          <a routerLink="/signup" class="inline-block mt-6 bg-white text-indigo-700 font-semibold px-6 py-3 rounded-lg hover:bg-indigo-50">Start your free trial</a>
        </div>
      </section>

      <!-- Footer -->
      <footer class="mt-auto border-t border-slate-100">
        <div class="max-w-6xl mx-auto px-5 py-8 flex flex-col sm:flex-row items-center justify-between gap-3 text-sm text-slate-500">
          <span class="font-bold text-slate-800">Wav<span class="text-indigo-600">Commerce</span></span>
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
  /** Signed-in staff landing on the apex need a way back into their console. */
  readonly auth = inject(AuthService);

  readonly plans = signal<PlanOption[]>([]);
  readonly year = 2026;

  /** Discount implied by the intro price, so "₹20 on a ₹1999 plan" reads as "90% off". */
  percentOff(p: PlanOption): number {
    if (p.introPriceInr == null || p.monthlyPrice <= 0) return 0;
    return Math.round(((p.monthlyPrice - p.introPriceInr) / p.monthlyPrice) * 100);
  }

  readonly features = [
    { icon: '🎨', title: 'Beautiful themes', body: 'Install a free theme and customise it — no code.' },
    { icon: '💳', title: 'Payments built in', body: 'Accept cards, UPI and COD out of the box.' },
    { icon: '🚚', title: 'Shipping & delivery', body: 'Zones, rates and courier integration.' },
    { icon: '🌐', title: 'Your own domain', body: 'Go live on your brand — connect a custom domain.' },
  ];

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'WavCommerce — Launch your online store in minutes',
      description: 'Build, run and grow your online store with WavCommerce. Themes, payments, shipping and your own domain. Start free — 14-day trial, no card required.',
    });
    this.onboarding.plans().subscribe((p) => this.plans.set(p));
  }
}
