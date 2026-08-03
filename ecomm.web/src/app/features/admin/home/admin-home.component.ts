import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';
import { AiAssistService } from '../../../core/services/ai-assist.service';
import { AuthService } from '../../../core/services/auth.service';
import { Dashboard, TestOrderResult } from '../../../core/models/admin-catalog.model';

@Component({
  selector: 'app-admin-home',
  imports: [RouterLink, DecimalPipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">{{ greeting() }}</h1>
      <p class="text-sm text-slate-500 mb-4">Here's how your store is doing.</p>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if (data(); as d) {
        <!-- Tabs: setup stays reachable for good, it doesn't vanish once complete -->
        <div class="flex gap-6 border-b border-slate-200 mb-6 text-sm">
          <button type="button" (click)="tab.set('dashboard')"
                  class="pb-2 -mb-px border-b-2 transition"
                  [class]="tab() === 'dashboard' ? 'border-primary text-primary font-medium' : 'border-transparent text-slate-500 hover:text-slate-700'">
            Dashboard
          </button>
          <button type="button" (click)="tab.set('setup')"
                  class="pb-2 -mb-px border-b-2 transition flex items-center gap-2"
                  [class]="tab() === 'setup' ? 'border-primary text-primary font-medium' : 'border-transparent text-slate-500 hover:text-slate-700'">
            Getting started
            @if (d.checklistDone < d.checklistTotal) {
              <span class="text-[11px] px-1.5 py-0.5 rounded-full bg-amber-100 text-amber-700 font-medium">{{ d.checklistTotal - d.checklistDone }}</span>
            }
          </button>
        </div>

        @if (tab() === 'dashboard') {
        <!-- KPI tiles -->
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          <div class="bg-white border border-slate-200 rounded-xl p-4">
            <div class="text-xs text-slate-500">Orders today</div>
            <div class="text-2xl font-bold text-slate-900 mt-1">{{ d.summary.ordersToday }}</div>
            <div class="text-xs text-slate-400">{{ d.summary.ordersThisWeek }} this week</div>
          </div>
          <div class="bg-white border border-slate-200 rounded-xl p-4">
            <div class="text-xs text-slate-500">Revenue today</div>
            <div class="text-2xl font-bold text-slate-900 mt-1">₹{{ d.summary.revenueToday | number:'1.0-0' }}</div>
            <div class="text-xs text-slate-400">₹{{ d.summary.revenueThisWeek | number:'1.0-0' }} this week</div>
          </div>
          <div class="bg-white border border-slate-200 rounded-xl p-4">
            <div class="text-xs text-slate-500">Avg order value (7d)</div>
            <div class="text-2xl font-bold text-slate-900 mt-1">₹{{ d.summary.aovThisWeek | number:'1.0-0' }}</div>
          </div>
          <div class="bg-white border border-slate-200 rounded-xl p-4">
            <div class="text-xs text-slate-500">New customers today</div>
            <div class="text-2xl font-bold text-slate-900 mt-1">{{ d.summary.newSignupsToday }}</div>
            <div class="text-xs text-slate-400">{{ d.summary.newSignupsThisWeek }} this week</div>
          </div>
        </div>

        <!-- Needs attention -->
        <div class="bg-white border border-slate-200 rounded-xl p-5">
          <h2 class="font-semibold text-slate-800 mb-3">Needs attention</h2>
          <div class="grid sm:grid-cols-2 gap-x-6">
            <ul class="space-y-2 text-sm">
              <li>
                <a routerLink="/admin/orders" class="flex items-center justify-between px-3 py-2 rounded-lg hover:bg-slate-50">
                  <span class="text-slate-600">Orders to fulfill</span>
                  <span class="font-semibold" [class]="d.summary.pendingActionCount ? 'text-amber-600' : 'text-slate-400'">{{ d.summary.pendingActionCount }}</span>
                </a>
              </li>
              <li>
                <a routerLink="/admin/inventory" class="flex items-center justify-between px-3 py-2 rounded-lg hover:bg-slate-50">
                  <span class="text-slate-600">Low-stock products</span>
                  <span class="font-semibold" [class]="d.summary.lowStockCount ? 'text-red-600' : 'text-slate-400'">{{ d.summary.lowStockCount }}</span>
                </a>
              </li>
            </ul>
            @if (d.summary.topSearches.length) {
              <div>
                <h3 class="text-xs font-medium text-slate-500 mb-2 px-3">Top searches</h3>
                <ul class="space-y-1 text-sm">
                  @for (t of d.summary.topSearches; track t.term) {
                    <li class="flex justify-between px-3 py-1"><span class="text-slate-600 truncate">{{ t.term }}</span><span class="text-slate-400">{{ t.count }}</span></li>
                  }
                </ul>
              </div>
            }
          </div>
        </div>
      } @else {
        <!-- Setup checklist -->
        <div class="bg-white border border-slate-200 rounded-xl p-5">
              <div class="flex items-center justify-between mb-1">
                <h2 class="font-semibold text-slate-800">Set up your store</h2>
                <span class="text-sm text-slate-500">{{ d.checklistDone }} / {{ d.checklistTotal }} done</span>
              </div>
              <div class="h-1.5 bg-slate-100 rounded-full overflow-hidden mb-4">
                <div class="h-full bg-primary rounded-full transition-all" [style.width.%]="(d.checklistDone / d.checklistTotal) * 100"></div>
              </div>
              <ul class="divide-y divide-slate-100">
                @for (item of d.checklist; track item.key) {
                  <li class="flex items-center gap-3 py-3">
                    <span class="w-5 h-5 rounded-full grid place-items-center text-xs shrink-0"
                          [class]="item.done ? 'bg-green-100 text-green-600' : 'border border-slate-300 text-transparent'">✓</span>
                    <div class="flex-1 min-w-0">
                      <div class="text-sm font-medium" [class]="item.done ? 'text-slate-400 line-through' : 'text-slate-800'">{{ item.label }}</div>
                      @if (!item.done) { <div class="text-xs text-slate-500">{{ item.description }}</div> }
                      @if (item.currentValue) { <div class="text-xs text-slate-400 truncate mt-0.5">{{ item.currentValue }}</div> }
                    </div>
                    @if (!item.done) {
                      <a [routerLink]="item.actionLink" class="text-sm text-primary hover:underline shrink-0">{{ item.actionLabel }} →</a>
                    }
                  </li>
                }
              </ul>
              @if (d.checklistDone === d.checklistTotal) {
                <p class="text-sm text-slate-500 mt-4 pt-4 border-t border-slate-100">🎉 Every setup step is done — your store is ready.</p>
              }
        </div>

        <!-- AI quick-start: only while there's no product yet — once "Add your first product" is done,
             running "Generate a catalog" again would dump AI-invented samples on top of a real store, and
             "Import your products" already has a permanent home (Products → Import/Export). -->
        @if (ai.enabled() && !stepDone(d, 'product')) {
          <div class="mt-6 rounded-xl border border-violet-200 bg-violet-50 p-5">
            <h2 class="font-semibold text-violet-900">✨ Quick start with AI</h2>
            <p class="text-sm text-violet-700/80 mt-0.5 mb-3">
              @if (stepDone(d, 'theme')) {
                Ready to add products? Let AI do the heavy lifting — you can edit everything afterwards.
              } @else {
                New store? Let AI do the heavy lifting — you can edit everything afterwards.
              }
            </p>
            <div class="grid sm:grid-cols-2 gap-3">
              <a routerLink="/admin/ai/catalog" class="block rounded-lg bg-white border border-violet-200 p-3 hover:border-violet-400 transition">
                <div class="text-sm font-medium text-slate-800">Generate a catalog</div>
                <div class="text-xs text-slate-500 mt-0.5">Pick a store type → products in seconds.</div>
              </a>
              <a routerLink="/admin/ai/import" class="block rounded-lg bg-white border border-violet-200 p-3 hover:border-violet-400 transition">
                <div class="text-sm font-medium text-slate-800">Import your products</div>
                <div class="text-xs text-slate-500 mt-0.5">Moving from Shopify/Woo/Wix or a spreadsheet.</div>
              </a>
            </div>
          </div>
        }

        <!-- Test order: prove the whole pipeline works before a real customer hits it -->
        <div class="mt-6 rounded-xl border border-emerald-200 bg-emerald-50 p-5">
          <div class="flex items-start gap-4">
            <span class="text-2xl leading-none">🧾</span>
            <div class="flex-1 min-w-0">
              <h2 class="font-semibold text-emerald-900">Try placing a test order</h2>
              <p class="text-sm text-emerald-800/80 mt-0.5">
                See the whole flow end to end — pricing, GST, shipping, the invoice and the confirmation email —
                exactly as a customer would trigger it.
              </p>
              @if (testOrder(); as t) {
                <div class="mt-3 rounded-lg bg-white border border-emerald-200 p-3 text-sm">
                  <div class="font-medium text-slate-800">{{ t.orderNumber }} — {{ t.productName }}</div>
                  <div class="text-xs text-slate-500 mt-0.5">
                    Total ₹{{ t.totalAmount | number:'1.0-2' }}. It's excluded from your analytics and holds one unit of stock —
                    cancel it to put that back.
                  </div>
                  <a [routerLink]="['/admin/orders', t.orderId]" class="inline-block mt-2 text-sm text-primary hover:underline">View the order →</a>
                </div>
              } @else {
                @if (testOrderError()) { <p class="text-sm text-red-700 mt-2">{{ testOrderError() }}</p> }
                <button type="button" (click)="placeTestOrder()" [disabled]="placingTestOrder()"
                        class="mt-3 px-3 py-2 rounded-lg bg-emerald-600 text-white text-sm font-medium hover:bg-emerald-700 disabled:opacity-60">
                  {{ placingTestOrder() ? 'Placing…' : 'Place a test order' }}
                </button>
              }
            </div>
          </div>
        </div>

        <!-- Feature discovery: built features merchants otherwise never find -->
        <div class="bg-white border border-slate-200 rounded-xl p-5 mt-6">
          <h2 class="font-semibold text-slate-800 mb-1">Ways to improve your store</h2>
          <p class="text-sm text-slate-500 mb-4">Already included in your plan — nothing extra to install.</p>
          <div class="grid sm:grid-cols-2 gap-x-6">
            @for (f of improvements; track f.link) {
              <a [routerLink]="f.link" class="flex items-start gap-3 py-3 group border-b border-slate-100 last:border-0">
                <span class="text-lg leading-none mt-0.5 shrink-0">{{ f.icon }}</span>
                <span class="flex-1 min-w-0">
                  <span class="block text-sm font-medium text-slate-800 group-hover:text-primary transition">{{ f.title }}</span>
                  <span class="block text-xs text-slate-500 mt-0.5">{{ f.blurb }}</span>
                </span>
                <span class="text-slate-300 group-hover:text-primary transition shrink-0">›</span>
              </a>
            }
          </div>
        </div>
        }
      } @else {
        <p class="text-slate-400 text-sm">Couldn't load the dashboard.</p>
      }
    </div>
  `,
})
export class AdminHomeComponent implements OnInit {
  private readonly api = inject(AdminCatalogService);
  private readonly auth = inject(AuthService);
  readonly ai = inject(AiAssistService);

  readonly loading = signal(true);
  readonly data = signal<Dashboard | null>(null);
  /** New stores land on setup; once it's complete the dashboard is the more useful default. */
  readonly tab = signal<'dashboard' | 'setup'>('dashboard');
  /** Shipped features that merchants miss because nothing points at them. */
  readonly improvements: { icon: string; title: string; blurb: string; link: string }[] = [
    { icon: '🏷️', title: 'Create discounts', blurb: 'Coupon codes, automatic offers and free shipping.', link: '/admin/coupons' },
    { icon: '🗂️', title: 'Group products into collections', blurb: 'Manual or rule-based, shown as storefront filters.', link: '/admin/collections' },
    { icon: '⭐', title: 'Collect customer reviews', blurb: 'Verified-buyer reviews with moderation.', link: '/admin/reviews' },
    { icon: '✉️', title: 'Customise emails & SMS', blurb: 'Edit the messages customers receive.', link: '/admin/notification-templates' },
    { icon: '📦', title: 'Track cost and suppliers', blurb: 'Record product cost to see real profit margins.', link: '/admin/suppliers' },
    { icon: '📈', title: 'See your sales analytics', blurb: 'Profit, margins, best-sellers and returns.', link: '/admin/analytics' },
  ];

  readonly greeting = computed(() => {
    const name = this.auth.currentUser()?.fullName?.split(' ')[0];
    return name ? `Welcome back, ${name}` : 'Welcome back';
  });

  /** Whether a specific setup-checklist step is done, by key — used to gate/word the AI quick-start
   *  panel off real store progress instead of the blunt "checklist isn't 100% done yet" check. */
  stepDone(d: Dashboard, key: string): boolean {
    return d.checklist.find((i) => i.key === key)?.done ?? false;
  }

  readonly testOrder = signal<TestOrderResult | null>(null);
  readonly placingTestOrder = signal(false);
  readonly testOrderError = signal<string | null>(null);

  placeTestOrder(): void {
    this.placingTestOrder.set(true);
    this.testOrderError.set(null);
    this.api.placeTestOrder().subscribe({
      next: (t) => { this.testOrder.set(t); this.placingTestOrder.set(false); },
      error: (e) => {
        this.testOrderError.set(e?.error?.message ?? 'Could not place the test order.');
        this.placingTestOrder.set(false);
      },
    });
  }

  ngOnInit(): void {
    this.ai.ensureStatus();
    this.api.getDashboard().subscribe({
      next: (d) => {
        this.data.set(d);
        if (d.checklistDone < d.checklistTotal) this.tab.set('setup');
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
