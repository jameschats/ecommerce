import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AnalyticsSummary, GroupProfitRow, ProductReportRow, ReturnRateRow, SalesPeriodRow } from '../../../core/models/analytics.model';
import { AnalyticsService } from '../../../core/services/analytics.service';

type Tab = 'sales' | 'best' | 'marginHigh' | 'marginLow' | 'return' | 'category' | 'supplier';

@Component({
  selector: 'app-admin-analytics',
  imports: [FormsModule, CurrencyPipe, RouterLink],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Analytics</h1>
      <p class="text-sm text-slate-500 mb-5">Business pulse + profit & margin reports. Margin = revenue − cost (tax excluded).</p>

      <!-- Activity widget -->
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

      <!-- Report controls -->
      <div class="flex flex-wrap items-center gap-2 mb-3">
        <div class="flex flex-wrap gap-1">
          @for (t of tabs; track t.key) {
            <button type="button" (click)="setTab(t.key)" class="px-3 py-1.5 rounded-lg text-sm border" [class]="tab() === t.key ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'">{{ t.label }}</button>
          }
        </div>
        <div class="flex items-center gap-1.5 ml-auto text-sm">
          <!-- Only meaningful for the time report; the others group by product. -->
          @if (tab() === 'sales') {
            <div class="flex rounded-lg border border-slate-200 p-0.5 bg-slate-50 mr-1">
              @for (b of buckets; track b) {
                <button type="button" (click)="setBucket(b)"
                        class="px-2.5 py-1 rounded-md capitalize transition"
                        [class]="bucket() === b ? 'bg-white shadow-sm text-slate-900 font-medium' : 'text-slate-500'">{{ b }}</button>
              }
            </div>
          }
          <input type="date" [(ngModel)]="from" (ngModelChange)="loadReport()" class="input py-1" />
          <span class="text-slate-400">to</span>
          <input type="date" [(ngModel)]="to" (ngModelChange)="loadReport()" class="input py-1" />
          <button type="button" (click)="exportCsv()" [disabled]="!rowCount()" class="px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50 disabled:opacity-50">CSV</button>
        </div>
      </div>

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!rowCount()) { <div class="p-8 text-center text-slate-400">No data for this range.</div> }
      @else {
        <div class="bg-white border border-slate-200 rounded-xl overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="text-left text-slate-400 border-b border-slate-100">
              <tr>
                <th class="px-4 py-2">{{ tab() === 'sales' ? 'Period' : isGroup() ? 'Group' : 'Product' }}</th>
                @if (tab() === 'sales') {
                  <th class="px-2 py-2 text-right">Orders</th><th class="px-2 py-2 text-right">Units</th>
                  <th class="px-4 py-2 w-1/3">Sales</th><th class="px-2 py-2 text-right">Profit</th><th class="px-2 py-2 text-right">Margin</th>
                } @else if (tab() === 'return') {
                  <th class="px-2 py-2 text-right">Sold</th><th class="px-2 py-2 text-right">Returned</th><th class="px-4 py-2 w-1/3">Return rate</th>
                } @else if (tab() === 'best') {
                  <th class="px-2 py-2 text-right">Units</th><th class="px-4 py-2 w-1/3">Revenue</th><th class="px-2 py-2 text-right">Profit</th>
                } @else {
                  <th class="px-2 py-2 text-right">Revenue</th><th class="px-2 py-2 text-right">Cost</th><th class="px-4 py-2 w-1/3">Profit</th><th class="px-2 py-2 text-right">Margin</th>
                }
              </tr>
            </thead>
            <tbody>
              @if (tab() === 'sales') {
                @for (r of salesRows(); track r.period) {
                  <!-- Months with no trade are shown rather than skipped: a gap that closes
                       up hides the quiet month instead of reporting it. -->
                  <tr class="border-b border-slate-50" [class.text-slate-400]="r.orders === 0">
                    <td class="px-4 py-2 font-medium">{{ r.label }}</td>
                    <td class="px-2 py-2 text-right">{{ r.orders }}</td>
                    <td class="px-2 py-2 text-right">{{ r.units }}</td>
                    <td class="px-4 py-2">
                      <span class="flex items-center gap-2">
                        <span class="h-2 rounded bg-blue-500/70" [style.width.%]="barPct(r.revenue, maxSales())"></span>
                        <span class="text-xs text-slate-600 whitespace-nowrap">{{ r.revenue | currency:'INR':'symbol':'1.0-0' }}</span>
                      </span>
                    </td>
                    <td class="px-2 py-2 text-right" [class.text-slate-400]="r.costMissing">
                      {{ r.costMissing ? '—' : (r.profit | currency:'INR':'symbol':'1.0-0') }}
                    </td>
                    <td class="px-2 py-2 text-right">{{ r.costMissing ? '—' : r.marginPct + '%' }}</td>
                  </tr>
                }
                @if (salesRows().length) {
                  <tr class="border-t-2 border-slate-200 font-semibold text-slate-900">
                    <td class="px-4 py-2">Total</td>
                    <td class="px-2 py-2 text-right">{{ salesTotals().orders }}</td>
                    <td class="px-2 py-2 text-right">{{ salesTotals().units }}</td>
                    <td class="px-4 py-2">{{ salesTotals().revenue | currency:'INR':'symbol':'1.0-0' }}</td>
                    <td class="px-2 py-2 text-right">{{ salesTotals().profit | currency:'INR':'symbol':'1.0-0' }}</td>
                    <td class="px-2 py-2 text-right"></td>
                  </tr>
                }
              } @else if (tab() === 'return') {
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
    { key: 'sales', label: 'Sales over time' },
    { key: 'best', label: 'Best sellers' },
    { key: 'marginHigh', label: 'Top margin' },
    { key: 'marginLow', label: 'Low margin' },
    { key: 'return', label: 'Return rate' },
    { key: 'category', label: 'By category' },
    { key: 'supplier', label: 'By supplier' },
  ];

  readonly summary = signal<AnalyticsSummary | null>(null);
  // Sales over time leads: it is the report a business reads first, and the one that was
  // missing entirely — every other report here groups by product rather than by time.
  readonly tab = signal<Tab>('sales');
  readonly loading = signal(false);
  readonly productRows = signal<ProductReportRow[]>([]);
  readonly returnRows = signal<ReturnRateRow[]>([]);
  readonly groupRows = signal<GroupProfitRow[]>([]);
  readonly salesRows = signal<SalesPeriodRow[]>([]);
  readonly bucket = signal<'month' | 'year' | 'day'>('month');
  readonly buckets: ('day' | 'month' | 'year')[] = ['day', 'month', 'year'];

  from = this.daysAgo(29);
  to = this.daysAgo(0);

  readonly maxSales = computed(() => Math.max(1, ...this.salesRows().map((r) => r.revenue)));

  readonly salesTotals = computed(() => this.salesRows().reduce(
    (a, r) => ({
      orders: a.orders + r.orders,
      units: a.units + r.units,
      revenue: a.revenue + r.revenue,
      profit: a.profit + r.profit,
    }),
    { orders: 0, units: 0, revenue: 0, profit: 0 }));

  /** Month or year needs a wider window than the 30-day default to say anything. */
  setBucket(b: 'month' | 'year' | 'day'): void {
    this.bucket.set(b);
    if (b === 'month' && this.from === this.daysAgo(29)) this.from = this.daysAgo(364);
    if (b === 'year') this.from = this.yearsAgo(3);
    this.loadReport();
  }

  private yearsAgo(n: number): string {
    const d = new Date();
    d.setFullYear(d.getFullYear() - n);
    return d.toISOString().slice(0, 10);
  }

  readonly isGroup = computed(() => this.tab() === 'category' || this.tab() === 'supplier');
  readonly rowCount = computed(() =>
    this.tab() === 'sales' ? this.salesRows().length
      : this.tab() === 'return' ? this.returnRows().length
      : this.isGroup() ? this.groupRows().length : this.productRows().length);
  readonly maxRevenue = computed(() => Math.max(1, ...this.productRows().map((r) => r.revenue)));
  readonly maxProfit = computed(() => Math.max(1, ...(this.isGroup() ? this.groupRows() : this.productRows()).map((r) => Math.max(0, r.profit))));
  readonly maxReturn = computed(() => Math.max(1, ...this.returnRows().map((r) => r.returnRatePct)));

  ngOnInit(): void {
    this.svc.summary().subscribe({ next: (s) => this.summary.set(s), error: () => {} });
    this.loadReport();
  }

  setTab(t: Tab): void { this.tab.set(t); this.loadReport(); }

  loadReport(): void {
    this.loading.set(true);
    const done = () => this.loading.set(false);
    const t = this.tab();
    if (t === 'sales') this.svc.salesOverTime(this.from, this.to, this.bucket()).subscribe({ next: (r) => { this.salesRows.set(r); done(); }, error: done });
    else if (t === 'best') this.svc.bestSellers(this.from, this.to).subscribe({ next: (r) => { this.productRows.set(r); done(); }, error: done });
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
    if (this.tab() === 'sales') {
      headers = ['Period', 'Orders', 'Units', 'Sales', 'Cost', 'Profit', 'Margin%'];
      rows = this.salesRows().map((r) => [r.label, r.orders, r.units, r.revenue, r.cost, r.profit, r.marginPct]);
    } else if (this.tab() === 'return') {
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
