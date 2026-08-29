import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface Funnel { sessionsViewed: number; sessionsAddedToCart: number; orders: number; viewToCartRate: number; cartToOrderRate: number; overallConversion: number; }
interface NamedCount { name: string; count: number; }
interface Traffic {
  pageviews: number; sessions: number; visitors: number; newVisitors: number; returningVisitors: number;
  bySource: NamedCount[]; byDevice: NamedCount[]; byCountry: NamedCount[]; byRegion: NamedCount[]; byCity: NamedCount[];
  topPages: NamedCount[]; series: { date: string; pageviews: number }[];
}
interface StorefrontAnalytics {
  views: number; addToCarts: number; orders: number; funnel: Funnel;
  topViewed: { productId: number; name: string; views: number }[];
  topSearches: { term: string; count: number }[];
  series: { date: string; views: number; addToCarts: number }[];
  traffic: Traffic;
}

/** Native storefront traffic + commerce analytics — owned first-party data, no third-party tracker. */
@Component({
  selector: 'app-admin-storefront-analytics',
  imports: [FormsModule, DatePipe, NgTemplateOutlet],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">🔎 Storefront analytics</h1>
        <select [(ngModel)]="days" (ngModelChange)="load()" class="input w-auto text-sm">
          <option [ngValue]="7">Last 7 days</option><option [ngValue]="30">Last 30 days</option><option [ngValue]="90">Last 90 days</option>
        </select>
      </div>
      <p class="text-sm text-slate-500 mb-5">Traffic and conversion from your own first-party data — no external tracker.</p>

      @if (data(); as d) {
        <!-- Traffic KPIs -->
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-6">
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Sessions</div><div class="text-2xl font-bold text-slate-900">{{ d.traffic.sessions }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Unique visitors</div><div class="text-2xl font-bold text-slate-900">{{ d.traffic.visitors }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Page views</div><div class="text-2xl font-bold text-slate-900">{{ d.traffic.pageviews }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">New / returning</div><div class="text-2xl font-bold text-slate-900">{{ d.traffic.newVisitors }} / {{ d.traffic.returningVisitors }}</div></div>
        </div>

        <!-- Sessions over time -->
        <div class="bg-white border border-slate-200 rounded-xl p-4 mb-6">
          <h2 class="font-semibold text-slate-800 mb-3">Page views over time</h2>
          <div class="flex items-end gap-0.5 h-24">
            @for (p of d.traffic.series; track p.date) {
              <div class="flex-1 bg-blue-200 rounded-t" [style.height.%]="bar(p.pageviews)" [title]="(p.date | date:'d MMM') + ': ' + p.pageviews"></div>
            }
          </div>
        </div>

        <div class="grid md:grid-cols-2 gap-6 mb-6">
          <div class="bg-white border border-slate-200 rounded-xl p-4"><h2 class="font-semibold text-slate-800 mb-3">By device</h2>{{ '' }}<ng-container [ngTemplateOutlet]="bars" [ngTemplateOutletContext]="{ rows: d.traffic.byDevice }" /></div>
          <div class="bg-white border border-slate-200 rounded-xl p-4"><h2 class="font-semibold text-slate-800 mb-3">By source</h2><ng-container [ngTemplateOutlet]="bars" [ngTemplateOutletContext]="{ rows: d.traffic.bySource }" /></div>
        </div>
        <div class="grid md:grid-cols-2 gap-6 mb-6">
          <div class="bg-white border border-slate-200 rounded-xl p-4"><h2 class="font-semibold text-slate-800 mb-3">Top pages</h2><ng-container [ngTemplateOutlet]="list" [ngTemplateOutletContext]="{ rows: d.traffic.topPages, empty: 'No page views yet.' }" /></div>
          <div class="bg-white border border-slate-200 rounded-xl p-4"><h2 class="font-semibold text-slate-800 mb-3">By city</h2><ng-container [ngTemplateOutlet]="list" [ngTemplateOutletContext]="{ rows: d.traffic.byCity, empty: 'No location data.' }" /></div>
        </div>
        <div class="grid md:grid-cols-2 gap-6 mb-8">
          <div class="bg-white border border-slate-200 rounded-xl p-4"><h2 class="font-semibold text-slate-800 mb-3">By region</h2><ng-container [ngTemplateOutlet]="list" [ngTemplateOutletContext]="{ rows: d.traffic.byRegion, empty: 'No location data.' }" /></div>
          <div class="bg-white border border-slate-200 rounded-xl p-4"><h2 class="font-semibold text-slate-800 mb-3">By country</h2><ng-container [ngTemplateOutlet]="list" [ngTemplateOutletContext]="{ rows: d.traffic.byCountry, empty: 'No location data.' }" /></div>
        </div>

        <!-- Commerce funnel -->
        <h2 class="text-lg font-bold text-slate-900 mb-3">Conversion funnel</h2>
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-6">
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Viewed products</div><div class="text-2xl font-bold text-slate-900">{{ d.funnel.sessionsViewed }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Added to cart</div><div class="text-2xl font-bold text-blue-600">{{ d.funnel.sessionsAddedToCart }}</div><div class="text-[11px] text-slate-400">{{ d.funnel.viewToCartRate }}%</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Orders</div><div class="text-2xl font-bold text-green-600">{{ d.funnel.orders }}</div><div class="text-[11px] text-slate-400">{{ d.funnel.cartToOrderRate }}% of carts</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Conversion</div><div class="text-2xl font-bold text-slate-900">{{ d.funnel.overallConversion }}%</div></div>
        </div>
        <div class="grid md:grid-cols-2 gap-6">
          <div class="bg-white border border-slate-200 rounded-xl p-4"><h2 class="font-semibold text-slate-800 mb-3">Most viewed products</h2>
            @if (!d.topViewed.length) { <p class="text-sm text-slate-400">No views yet.</p> }
            <ul class="divide-y divide-slate-100">@for (p of d.topViewed; track p.productId) { <li class="py-2 flex justify-between text-sm"><span class="text-slate-700 truncate">{{ p.name }}</span><span class="text-slate-500">{{ p.views }}</span></li> }</ul>
          </div>
          <div class="bg-white border border-slate-200 rounded-xl p-4"><h2 class="font-semibold text-slate-800 mb-3">Top searches</h2>
            @if (!d.topSearches.length) { <p class="text-sm text-slate-400">No searches yet.</p> }
            <ul class="divide-y divide-slate-100">@for (s of d.topSearches; track s.term) { <li class="py-2 flex justify-between text-sm"><span class="text-slate-700 truncate">{{ s.term }}</span><span class="text-slate-500">{{ s.count }}</span></li> }</ul>
          </div>
        </div>
      } @else if (loaded()) {
        <p class="text-slate-400">No data yet — analytics appear once your storefront has visitors.</p>
      }
    </div>

    <ng-template #bars let-rows="rows">
      @if (!rows.length) { <p class="text-sm text-slate-400">No data.</p> }
      @for (r of rows; track r.name) {
        <div class="mb-2">
          <div class="flex justify-between text-sm"><span class="text-slate-700 truncate">{{ r.name }}</span><span class="text-slate-500">{{ r.count }} ({{ pct(r.count, rows) }}%)</span></div>
          <div class="h-1.5 bg-slate-100 rounded mt-0.5"><div class="h-1.5 bg-blue-400 rounded" [style.width.%]="pct(r.count, rows)"></div></div>
        </div>
      }
    </ng-template>
    <ng-template #list let-rows="rows" let-empty="empty">
      @if (!rows.length) { <p class="text-sm text-slate-400">{{ empty }}</p> }
      <ul class="divide-y divide-slate-100">@for (r of rows; track r.name) { <li class="py-1.5 flex justify-between text-sm"><span class="text-slate-700 truncate">{{ r.name }}</span><span class="text-slate-500">{{ r.count }}</span></li> }</ul>
    </ng-template>
  `,
})
export class AdminStorefrontAnalyticsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  readonly data = signal<StorefrontAnalytics | null>(null);
  readonly loaded = signal(false);
  days = 30;
  private maxPv = 1;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.http.get<ApiResponse<StorefrontAnalytics>>(`${API_BASE_URL}/admin/analytics/storefront?days=${this.days}`).subscribe({
      next: (r) => { const d = r.data ?? null; this.data.set(d); this.maxPv = Math.max(1, ...(d?.traffic.series.map((p) => p.pageviews) ?? [1])); this.loaded.set(true); },
      error: () => this.loaded.set(true),
    });
  }
  bar(v: number): number { return Math.max(2, Math.round((v / this.maxPv) * 100)); }
  pct(v: number, rows: NamedCount[]): number { const t = rows.reduce((s, r) => s + r.count, 0); return t > 0 ? Math.round((v / t) * 100) : 0; }
}
