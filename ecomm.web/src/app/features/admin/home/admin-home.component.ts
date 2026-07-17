import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';
import { AiAssistService } from '../../../core/services/ai-assist.service';
import { AuthService } from '../../../core/services/auth.service';
import { Dashboard } from '../../../core/models/admin-catalog.model';

@Component({
  selector: 'app-admin-home',
  imports: [RouterLink, DecimalPipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">{{ greeting() }}</h1>
      <p class="text-sm text-slate-500 mb-6">Here's how your store is doing.</p>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if (data(); as d) {
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

        <!-- AI quick-start (new stores only) -->
        @if (ai.enabled() && d.checklistDone < d.checklistTotal) {
          <div class="mb-6 rounded-xl border border-violet-200 bg-violet-50 p-5">
            <h2 class="font-semibold text-violet-900">✨ Quick start with AI</h2>
            <p class="text-sm text-violet-700/80 mt-0.5 mb-3">New store? Let AI do the heavy lifting — you can edit everything afterwards.</p>
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

        <div class="grid lg:grid-cols-3 gap-6">
          <!-- Setup checklist -->
          @if (d.checklistDone < d.checklistTotal) {
            <div class="lg:col-span-2 bg-white border border-slate-200 rounded-xl p-5">
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
                    </div>
                    @if (!item.done) {
                      <a [routerLink]="item.actionLink" class="text-sm text-primary hover:underline shrink-0">{{ item.actionLabel }} →</a>
                    }
                  </li>
                }
              </ul>
            </div>
          } @else {
            <div class="lg:col-span-2 bg-white border border-slate-200 rounded-xl p-5">
              <h2 class="font-semibold text-slate-800">🎉 Your store is set up</h2>
              <p class="text-sm text-slate-500 mt-1">All setup steps are complete. Keep an eye on the needs-attention list.</p>
            </div>
          }

          <!-- Needs attention -->
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <h2 class="font-semibold text-slate-800 mb-3">Needs attention</h2>
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
              <h3 class="text-xs font-medium text-slate-500 mt-4 mb-2">Top searches</h3>
              <ul class="space-y-1 text-sm">
                @for (t of d.summary.topSearches; track t.term) {
                  <li class="flex justify-between px-3 py-1"><span class="text-slate-600 truncate">{{ t.term }}</span><span class="text-slate-400">{{ t.count }}</span></li>
                }
              </ul>
            }
          </div>
        </div>
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
  readonly greeting = computed(() => {
    const name = this.auth.currentUser()?.fullName?.split(' ')[0];
    return name ? `Welcome back, ${name}` : 'Welcome back';
  });

  ngOnInit(): void {
    this.ai.ensureStatus();
    this.api.getDashboard().subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
