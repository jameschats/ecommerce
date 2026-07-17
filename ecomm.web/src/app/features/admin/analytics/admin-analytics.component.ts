import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AnalyticsSummary, GroupProfitRow, ProductReportRow, ReturnRateRow, SalesDashboard } from '../../../core/models/analytics.model';
import { AnalyticsService } from '../../../core/services/analytics.service';

type Tab = 'best' | 'marginHigh' | 'marginLow' | 'return' | 'category' | 'supplier';

@Component({
  selector: 'app-admin-analytics',
  imports: [FormsModule, CurrencyPipe, RouterLink],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex flex-wrap items-end justify-between gap-3 mb-5">
        <div>
          <h1 class="text-xl font-bold text-slate-900 mb-1">Analytics</h1>
          <p class="text-sm text-slate-500">Sales overview + profit & margin reports. Margin = revenue − cost (tax excluded).</p>
        </div>
        <div class="flex items-center gap-1.5 text-sm">
          <input type="date" [(ngModel)]="from" (ngModelChange)="reload()" class="input py-1" />
          <span class="text-slate-400">to</span>
          <input type="date" [(ngModel)]="to" (ngModelChange)="reload()" class="input py-1" />
        </div>
      </div>

      <!-- Business-pulse widget (today / this week) -->
      @if (summary(); as s) {
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 mb-6">
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Orders today</div><div class="text-lg font-bold text-slate-900">{{ s.ordersToday }}</div><div class="text-[11px] text-slate-400">{{ s.ordersThisWeek }} this week</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Revenue today</div><div class="text-lg font-bold text-slate-900">{{ s.revenueToday | currency:'INR':'symbol':'1.0-0' }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">AOV (7d)</div><div class="text-lg font-bold text-slate-900">{{ s.aovThisWeek | currency:'INR':'symbol':'1.0-0' }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">New signups (7d)</div><div class="text-lg font-bold text-slate-900">{{ s.newSignupsThisWeek }}</div></div>
          <div class="bg-white border rounded-xl p-3" [class]="s.pendingActionCount > 0 ? 'border-amber-200 bg-amber-50' : 'border-slate-200 bg-white'"><div class="text-xs text-slate-400">Needs action</div><div class="text-lg font-bold text-slate-900">{{ s.pendingActionCount }}</div><div class="text-[11px] text-slate-400">paid, not shipped</div></div>
          <div class="bg-white border rounded-xl p-3" [class]="s.lowStockCount > 0 ? 'border-red-200 bg-red-50' : 'border-slate-200 bg-white'"><div class="text-xs text-slate-400">Low stock</div><div class="text-lg font-bold text-slate-900">{{ s.lowStockCount }}</div></div>
        </div>
        @if (s.topSearches.length) {
          <div class="text-xs text-slate-500 mb-6">Top searches: @for (t of s.topSearches; track t.term) { <span class="inline-block bg-slate-100 rounded px-2 py-0.5 mr-1.5">{{ t.term }} ({{ t.count }})</span> }</div>
        }
      }

      <!-- Sales dashboard (range-based) -->
      @if (sales(); as sd) {
        <div class="grid grid-cols-2 lg:grid-cols-5 gap-3 mb-4">
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Net sales</div><div class="text-lg font-bold text-slate-900">{{ sd.kpis.netSales | currency:'INR':'symbol':'1.0-0' }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Gross sales</div><div class="text-lg font-bold text-slate-900">{{ sd.kpis.grossSales | currency:'INR':'symbol':'1.0-0' }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Orders</div><div class="text-lg font-bold text-slate-900">{{ sd.kpis.orders }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Avg order value</div><div class="text-lg font-bold text-slate-900">{{ sd.kpis.aov | currency:'INR':'symbol':'1.0-0' }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Returning customers</div><div class="text-lg font-bold text-slate-900">{{ sd.kpis.returningRatePct }}%</div><div class="text-[11px] text-slate-400">of buyers in range</div></div>
        </div>

        <div class="grid lg:grid-cols-3 gap-4 mb-6">
          <!-- Sales over time -->
          <div class="lg:col-span-2 bg-white border border-slate-200 rounded-xl p-4">
            <div class="flex items-baseline justify-between mb-2">
              <div class="text-sm font-medium text-slate-700">Sales over time</div>
              <div class="text-lg font-bold text-slate-900">{{ sd.kpis.netSales | currency:'INR':'symbol':'1.0-0' }}</div>
            </div>
            @if (chart(); as c) {
              <svg [attr.viewBox]="'0 0 ' + c.w + ' ' + c.h" preserveAspectRatio="none" class="w-full h-44">
                <polygon [attr.points]="c.area" fill="#3b82f6" opacity="0.10" />
                <polyline [attr.points]="c.line" fill="none" stroke="#2563eb" stroke-width="2" vector-effect="non-scaling-stroke" stroke-linejoin="round" stroke-linecap="round" />
              </svg>
              <div class="flex justify-between text-[11px] text-slate-400 mt-1">
                <span>{{ c.first }}</span>
                <span>peak {{ c.peak.sales | currency:'INR':'symbol':'1.0-0' }}</span>
                <span>{{ c.last }}</span>
              </div>
            } @else {
              <div class="h-44 flex items-center justify-center text-slate-400 text-sm">No sales in this range.</div>
            }
          </div>

          <!-- Sales breakdown -->
          <div class="bg-white border border-slate-200 rounded-xl p-4">
            <div class="text-sm font-medium text-slate-700 mb-3">Breakdown</div>
            <dl class="space-y-1.5 text-sm">
              <div class="flex justify-between"><dt class="text-slate-500">Gross sales</dt><dd class="text-slate-800">{{ sd.breakdown.gross | currency:'INR':'symbol':'1.0-0' }}</dd></div>
              <div class="flex justify-between"><dt class="text-slate-500">Discounts</dt><dd class="text-rose-600">−{{ sd.breakdown.discounts | currency:'INR':'symbol':'1.0-0' }}</dd></div>
              <div class="flex justify-between"><dt class="text-slate-500">Returns</dt><dd class="text-rose-600">−{{ sd.breakdown.returns | currency:'INR':'symbol':'1.0-0' }}</dd></div>
              <div class="flex justify-between border-t border-slate-100 pt-1.5 font-medium"><dt class="text-slate-600">Net sales</dt><dd class="text-slate-900">{{ sd.breakdown.net | currency:'INR':'symbol':'1.0-0' }}</dd></div>
              <div class="flex justify-between"><dt class="text-slate-500">Shipping</dt><dd class="text-slate-800">{{ sd.breakdown.shipping | currency:'INR':'symbol':'1.0-0' }}</dd></div>
              <div class="flex justify-between"><dt class="text-slate-500">Tax</dt><dd class="text-slate-800">{{ sd.breakdown.tax | currency:'INR':'symbol':'1.0-0' }}</dd></div>
              <div class="flex justify-between border-t border-slate-100 pt-1.5 font-semibold"><dt class="text-slate-700">Total</dt><dd class="text-slate-900">{{ sd.breakdown.total | currency:'INR':'symbol':'1.0-0' }}</dd></div>
            </dl>
          </div>
        </div>

        <!-- New vs returning -->
        <div class="grid grid-cols-2 gap-4 mb-8">
          <div class="bg-white border border-slate-200 rounded-xl p-4">
            <div class="text-xs text-slate-400">New customers</div>
            <div class="text-2xl font-bold text-slate-900">{{ sd.newVsReturning.newCustomers }}</div>
            <div class="text-[11px] text-slate-400">{{ sd.newVsReturning.newRevenue | currency:'INR':'symbol':'1.0-0' }} revenue</div>
          </div>
          <div class="bg-white border border-slate-200 rounded-xl p-4">
            <div class="text-xs text-slate-400">Returning customers</div>
            <div class="text-2xl font-bold text-slate-900">{{ sd.newVsReturning.returningCustomers }}</div>
            <div class="text-[11px] text-slate-400">{{ sd.newVsReturning.returningRevenue | currency:'INR':'symbol':'1.0-0' }} revenue</div>
          </div>
        </div>
      }

      <!-- Report controls -->
      <div class="flex flex-wrap items-center gap-2 mb-3">
        <div class="flex flex-wrap gap-1">
          @for (t of tabs; track t.key) {
            <button type="button" (click)="setTab(t.key)" class="px-3 py-1.5 rounded-lg text-sm border" [class]="tab() === t.key ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'">{{ t.label }}</button>
          }
        </div>
        <button type="button" (click)="exportCsv()" [disabled]="!rowCount()" class="ml-auto px-3 py-1.5 rounded-lg border border-slate-300 text-sm hover:bg-slate-50 disabled:opacity-50">CSV</button>
      </div>

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!rowCount()) { <div class="p-8 text-center text-slate-400">No data for this range.</div> }
      @else {
        <div class="bg-white border border-slate-200 rounded-xl overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="text-left text-slate-400 border-b border-slate-100">
              <tr>
                <th class="px-4 py-2">{{ isGroup() ? 'Group' : 'Product' }}</th>
                @if (tab() === 'return') {
                  <th class="px-2 py-2 text-right">Sold</th><th class="px-2 py-2 text-right">Returned</th><th class="px-4 py-2 w-1/3">Return rate</th>
                } @else if (tab() === 'best') {
                  <th class="px-2 py-2 text-right">Units</th><th class="px-4 py-2 w-1/3">Revenue</th><th class="px-2 py-2 text-right">Profit</th>
                } @else {
                  <th class="px-2 py-2 text-right">Revenue</th><th class="px-2 py-2 text-right">Cost</th><th class="px-4 py-2 w-1/3">Profit</th><th class="px-2 py-2 text-right">Margin</th>
                }
              </tr>
            </thead>
            <tbody>
              @if (tab() === 'return') {
                @for (r of returnRows(); track r.productId) {
                  <tr class="border-b border-slate-50">
                    <td class="px-4 py-2 text-slate-800">{{ r.name }}</td>
                    <td class="px-2 py-2 text-right">{{ r.sold }}</td>
                    <td class="px-2 py-2 text-right">{{ r.returned }}</td>
                    <td class="px-4 py-2"><span class="flex items-center gap-2"><span class="h-2 rounded bg-blue-500/70" [style.width.%]="barPct(r.returnRatePct, maxReturn())"></span><span class="text-xs text-slate-600 whitespace-nowrap">{{ r.returnRatePct }}%</span></span></td>
                  </tr>
                }
              } @else if (tab() === 'best') {
                @for (r of productRows(); track r.productId) {
                  <tr class="border-b border-slate-50">
                    <td class="px-4 py-2 text-slate-800">{{ r.name }}</td>
                    <td class="px-2 py-2 text-right">{{ r.units }}</td>
                    <td class="px-4 py-2"><span class="flex items-center gap-2"><span class="h-2 rounded bg-blue-500/70" [style.width.%]="barPct(r.revenue, maxRevenue())"></span><span class="text-xs text-slate-600 whitespace-nowrap">{{ r.revenue | currency:'INR':'symbol':'1.0-0' }}</span></span></td>
                    <td class="px-2 py-2 text-right" [class.text-slate-400]="r.costMissing">{{ r.costMissing ? '—' : (r.profit | currency:'INR':'symbol':'1.0-0') }}</td>
                  </tr>
                }
              } @else if (isGroup()) {
                @for (r of groupRows(); track r.name) {
                  <tr class="border-b border-slate-50">
                    <td class="px-4 py-2 text-slate-800">{{ r.name }}</td>
                    <td class="px-2 py-2 text-right">{{ r.revenue | currency:'INR':'symbol':'1.0-0' }}</td>
                    <td class="px-2 py-2 text-right">{{ r.cost | currency:'INR':'symbol':'1.0-0' }}</td>
                    <td class="px-4 py-2"><span class="flex items-center gap-2"><span class="h-2 rounded bg-blue-500/70" [style.width.%]="barPct(r.profit, maxProfit())"></span><span class="text-xs text-slate-600 whitespace-nowrap">{{ r.profit | currency:'INR':'symbol':'1.0-0' }}</span></span></td>
                    <td class="px-2 py-2 text-right">{{ r.marginPct }}%</td>
                  </tr>
                }
              } @else {
                @for (r of productRows(); track r.productId) {
                  <tr class="border-b border-slate-50">
                    <td class="px-4 py-2 text-slate-800">{{ r.name }} @if (r.costMissing) { <span class="text-[10px] text-amber-600 bg-amber-50 border border-amber-200 rounded px-1">no cost</span> }</td>
                    <td class="px-2 py-2 text-right">{{ r.revenue | currency:'INR':'symbol':'1.0-0' }}</td>
                    <td class="px-2 py-2 text-right">{{ r.cost | currency:'INR':'symbol':'1.0-0' }}</td>
                    <td class="px-4 py-2"><span class="flex items-center gap-2"><span class="h-2 rounded bg-blue-500/70" [style.width.%]="barPct(r.profit, maxProfit())"></span><span class="text-xs text-slate-600 whitespace-nowrap">{{ r.profit | currency:'INR':'symbol':'1.0-0' }}</span></span></td>
                    <td class="px-2 py-2 text-right font-medium" [class]="r.marginPct < 15 ? 'text-red-600' : 'text-slate-800'">{{ r.marginPct }}%</td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
        <p class="text-[11px] text-slate-400 mt-2">Revenue is net (tax excluded). Cost uses the price at sale time; “no cost” rows have no cost price set — <a routerLink="/admin/products" class="underline">add cost prices</a> for accurate margins.</p>
      }
    </div>
  `,
})
export class AdminAnalyticsComponent implements OnInit {
  private readonly svc = inject(AnalyticsService);

  readonly tabs: { key: Tab; label: string }[] = [
    { key: 'best', label: 'Best sellers' },
    { key: 'marginHigh', label: 'Top margin' },
    { key: 'marginLow', label: 'Low margin' },
    { key: 'return', label: 'Return rate' },
    { key: 'category', label: 'By category' },
    { key: 'supplier', label: 'By supplier' },
  ];

  readonly summary = signal<AnalyticsSummary | null>(null);
  readonly sales = signal<SalesDashboard | null>(null);
  readonly tab = signal<Tab>('best');
  readonly loading = signal(false);
  readonly productRows = signal<ProductReportRow[]>([]);
  readonly returnRows = signal<ReturnRateRow[]>([]);
  readonly groupRows = signal<GroupProfitRow[]>([]);

  from = this.daysAgo(29);
  to = this.daysAgo(0);

  readonly isGroup = computed(() => this.tab() === 'category' || this.tab() === 'supplier');
  readonly rowCount = computed(() =>
    this.tab() === 'return' ? this.returnRows().length : this.isGroup() ? this.groupRows().length : this.productRows().length);
  readonly maxRevenue = computed(() => Math.max(1, ...this.productRows().map((r) => r.revenue)));
  readonly maxProfit = computed(() => Math.max(1, ...(this.isGroup() ? this.groupRows() : this.productRows()).map((r) => Math.max(0, r.profit))));
  readonly maxReturn = computed(() => Math.max(1, ...this.returnRows().map((r) => r.returnRatePct)));

  // Hand-rolled SSR-safe SVG line chart: fills every day in the range (0 when no sales) so gaps read correctly.
  readonly chart = computed(() => {
    const sd = this.sales();
    if (!sd || !sd.series.length) return null;
    const byDate = new Map<string, number>();
    for (const p of sd.series) byDate.set(p.date.slice(0, 10), p.sales);

    const days: { date: string; sales: number }[] = [];
    const cur = new Date(this.from + 'T00:00:00');
    const end = new Date(this.to + 'T00:00:00');
    let guard = 0;
    while (cur <= end && guard++ < 400) {
      const key = `${cur.getFullYear()}-${String(cur.getMonth() + 1).padStart(2, '0')}-${String(cur.getDate()).padStart(2, '0')}`;
      days.push({ date: key, sales: byDate.get(key) ?? 0 });
      cur.setDate(cur.getDate() + 1);
    }
    if (!days.length) return null;

    const w = 700, h = 180, padX = 6, padTop = 12, padBot = 6;
    const max = Math.max(1, ...days.map((d) => d.sales));
    const n = days.length;
    const x = (i: number) => (n <= 1 ? w / 2 : padX + (i / (n - 1)) * (w - 2 * padX));
    const y = (v: number) => h - padBot - (v / max) * (h - padTop - padBot);
    const pts = days.map((d, i) => `${x(i).toFixed(1)},${y(d.sales).toFixed(1)}`);
    const line = pts.join(' ');
    const baseY = (h - padBot).toFixed(1);
    const area = `${x(0).toFixed(1)},${baseY} ${line} ${x(n - 1).toFixed(1)},${baseY}`;
    const peak = days.reduce((a, b) => (b.sales > a.sales ? b : a), days[0]);
    return { line, area, w, h, first: days[0].date, last: days[n - 1].date, peak };
  });

  ngOnInit(): void {
    this.svc.summary().subscribe({ next: (s) => this.summary.set(s), error: () => {} });
    this.reload();
  }

  setTab(t: Tab): void { this.tab.set(t); this.loadReport(); }

  /** Refresh both the sales dashboard and the active report for the current date range. */
  reload(): void {
    this.loadSales();
    this.loadReport();
  }

  loadSales(): void {
    this.svc.salesDashboard(this.from, this.to).subscribe({ next: (d) => this.sales.set(d), error: () => this.sales.set(null) });
  }

  loadReport(): void {
    this.loading.set(true);
    const done = () => this.loading.set(false);
    const t = this.tab();
    if (t === 'best') this.svc.bestSellers(this.from, this.to).subscribe({ next: (r) => { this.productRows.set(r); done(); }, error: done });
    else if (t === 'marginHigh') this.svc.margins(this.from, this.to, 'high').subscribe({ next: (r) => { this.productRows.set(r); done(); }, error: done });
    else if (t === 'marginLow') this.svc.margins(this.from, this.to, 'low').subscribe({ next: (r) => { this.productRows.set(r); done(); }, error: done });
    else if (t === 'return') this.svc.returnRate(this.from, this.to).subscribe({ next: (r) => { this.returnRows.set(r); done(); }, error: done });
    else if (t === 'category') this.svc.profitByCategory(this.from, this.to).subscribe({ next: (r) => { this.groupRows.set(r); done(); }, error: done });
    else this.svc.profitBySupplier(this.from, this.to).subscribe({ next: (r) => { this.groupRows.set(r); done(); }, error: done });
  }

  barPct(value: number, max: number): number {
    return Math.max(2, Math.round((Math.max(0, value) / max) * 100));
  }

  exportCsv(): void {
    let headers: string[]; let rows: (string | number)[][];
    if (this.tab() === 'return') {
      headers = ['Product', 'Sold', 'Returned', 'ReturnRate%'];
      rows = this.returnRows().map((r) => [r.name, r.sold, r.returned, r.returnRatePct]);
    } else if (this.isGroup()) {
      headers = ['Group', 'Revenue', 'Cost', 'Profit', 'Margin%'];
      rows = this.groupRows().map((r) => [r.name, r.revenue, r.cost, r.profit, r.marginPct]);
    } else {
      headers = ['Product', 'Units', 'Revenue', 'Cost', 'Profit', 'Margin%', 'CostMissing'];
      rows = this.productRows().map((r) => [r.name, r.units, r.revenue, r.cost, r.profit, r.marginPct, r.costMissing ? 'yes' : '']);
    }
    const esc = (v: string | number) => `"${String(v).replace(/"/g, '""')}"`;
    const csv = [headers, ...rows].map((r) => r.map(esc).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `analytics-${this.tab()}-${this.from}_${this.to}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  }

  private daysAgo(n: number): string {
    const d = new Date();
    d.setDate(d.getDate() - n);
    return d.toISOString().slice(0, 10);
  }
}
