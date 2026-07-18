import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { PlatformAnalytics } from '../../core/models/superadmin.model';

/** Cross-store platform analytics: GMV, orders, active stores, collected revenue + a store leaderboard. */
@Component({
  selector: 'app-superadmin-analytics',
  imports: [FormsModule, RouterLink, DecimalPipe],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3 mb-4">
      <div>
        <h1 class="text-xl font-bold text-slate-900">Platform analytics</h1>
        <p class="text-sm text-slate-500">GMV across all stores (paid orders). Collected = actual subscription charges.</p>
      </div>
      <div class="flex items-center gap-1.5 text-sm">
        <input type="date" [(ngModel)]="from" (ngModelChange)="load()" class="input py-1" />
        <span class="text-slate-400">to</span>
        <input type="date" [(ngModel)]="to" (ngModelChange)="load()" class="input py-1" />
      </div>
    </div>

    @if (data(); as d) {
      <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 mb-6">
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Platform GMV</div><div class="text-lg font-bold text-slate-900">₹{{ d.gmv | number:'1.0-0' }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Orders</div><div class="text-lg font-bold">{{ d.orders }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">AOV</div><div class="text-lg font-bold">₹{{ d.aov | number:'1.0-0' }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Active stores</div><div class="text-lg font-bold text-green-600">{{ d.activeStores }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">New stores</div><div class="text-lg font-bold text-blue-600">{{ d.newStores }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Collected</div><div class="text-lg font-bold text-slate-900">₹{{ d.collectedRevenue | number:'1.0-0' }}</div></div>
      </div>

      <div class="grid lg:grid-cols-3 gap-4">
        <div class="lg:col-span-2 bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-sm font-medium text-slate-700 mb-2">GMV over time</div>
          @if (chart(); as c) {
            <svg [attr.viewBox]="'0 0 ' + c.w + ' ' + c.h" preserveAspectRatio="none" class="w-full h-44">
              <polygon [attr.points]="c.area" fill="#16a34a" opacity="0.10" />
              <polyline [attr.points]="c.line" fill="none" stroke="#16a34a" stroke-width="2" vector-effect="non-scaling-stroke" stroke-linejoin="round" />
            </svg>
            <div class="flex justify-between text-[11px] text-slate-400 mt-1"><span>{{ c.first }}</span><span>peak ₹{{ c.peak | number:'1.0-0' }}</span><span>{{ c.last }}</span></div>
          } @else {
            <div class="h-44 flex items-center justify-center text-slate-400 text-sm">No sales in this range.</div>
          }
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-sm font-medium text-slate-700 mb-2">Top stores by GMV</div>
          <table class="w-full text-sm">
            <tbody>
              @for (s of d.topStores; track s.tenantId; let i = $index) {
                <tr class="border-b border-slate-50">
                  <td class="py-1.5 text-slate-400 w-5">{{ i + 1 }}</td>
                  <td class="py-1.5"><a [routerLink]="['/superadmin/tenants', s.tenantId]" class="text-slate-800 hover:text-blue-600">{{ s.name }}</a><div class="text-[11px] text-slate-400">{{ s.orders }} orders</div></td>
                  <td class="py-1.5 text-right font-medium text-slate-800">₹{{ s.gmv | number:'1.0-0' }}</td>
                </tr>
              }
              @if (!d.topStores.length) { <tr><td class="py-6 text-center text-slate-400">No sales yet.</td></tr> }
            </tbody>
          </table>
        </div>
      </div>
    }
  `,
})
export class SuperAdminAnalyticsComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly data = signal<PlatformAnalytics | null>(null);
  from = this.daysAgo(29);
  to = this.daysAgo(0);

  readonly chart = computed(() => {
    const d = this.data();
    if (!d || !d.series.length) return null;
    const byDate = new Map<string, number>(d.series.map((p) => [p.date.slice(0, 10), p.gmv]));
    const days: number[] = [];
    const cur = new Date(this.from + 'T00:00:00');
    const end = new Date(this.to + 'T00:00:00');
    let guard = 0;
    const labels: string[] = [];
    while (cur <= end && guard++ < 400) {
      const key = `${cur.getFullYear()}-${String(cur.getMonth() + 1).padStart(2, '0')}-${String(cur.getDate()).padStart(2, '0')}`;
      days.push(byDate.get(key) ?? 0);
      labels.push(key);
      cur.setDate(cur.getDate() + 1);
    }
    const w = 700, h = 180, padX = 6, padTop = 12, padBot = 6;
    const max = Math.max(1, ...days);
    const n = days.length;
    const x = (i: number) => (n <= 1 ? w / 2 : padX + (i / (n - 1)) * (w - 2 * padX));
    const y = (v: number) => h - padBot - (v / max) * (h - padTop - padBot);
    const pts = days.map((v, i) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`);
    const baseY = (h - padBot).toFixed(1);
    return {
      line: pts.join(' '),
      area: `${x(0).toFixed(1)},${baseY} ${pts.join(' ')} ${x(n - 1).toFixed(1)},${baseY}`,
      w, h, first: labels[0], last: labels[n - 1], peak: max,
    };
  });

  ngOnInit(): void { this.load(); }
  load(): void { this.svc.analytics(this.from, this.to).subscribe((d) => this.data.set(d)); }

  private daysAgo(nn: number): string {
    const d = new Date();
    d.setDate(d.getDate() - nn);
    return d.toISOString().slice(0, 10);
  }
}
