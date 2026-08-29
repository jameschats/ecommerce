import { Component, OnInit, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { PlatformRevenue } from '../../core/models/superadmin.model';

/** Platform revenue: MRR + subscription-status counts + per-plan breakdown. */
@Component({
  selector: 'app-superadmin-revenue',
  imports: [DecimalPipe],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-4">Revenue</h1>
    @if (revenue(); as r) {
      <div class="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-7 gap-3 mb-6">
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">MRR</div><div class="text-xl font-bold text-slate-900">₹{{ r.mrr | number:'1.0-0' }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Stores</div><div class="text-xl font-bold">{{ r.totalTenants }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Active</div><div class="text-xl font-bold text-green-600">{{ r.active }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Trial</div><div class="text-xl font-bold text-blue-600">{{ r.trial }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Past due</div><div class="text-xl font-bold text-amber-600">{{ r.pastDue }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Suspended</div><div class="text-xl font-bold text-red-500">{{ r.suspended }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Cancelled</div><div class="text-xl font-bold text-slate-500">{{ r.cancelled }}</div></div>
      </div>

      <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3 mb-6">
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">ARPU</div><div class="text-xl font-bold text-slate-900">₹{{ r.arpu | number:'1.0-0' }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Churn (30d)</div><div class="text-xl font-bold text-slate-900">{{ r.churnRatePercent | number:'1.0-1' }}%</div><div class="text-[11px] text-slate-400">{{ r.churnedLast30 }} cancelled</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Collected (30d)</div><div class="text-xl font-bold text-green-600">₹{{ r.collectedLast30 | number:'1.0-0' }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Collected (90d)</div><div class="text-xl font-bold text-green-700">₹{{ r.collectedLast90 | number:'1.0-0' }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Annualized (ARR)</div><div class="text-xl font-bold text-slate-900">₹{{ r.mrr * 12 | number:'1.0-0' }}</div></div>
      </div>

      <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-xl">
        <h2 class="font-semibold text-slate-800 mb-3">MRR by plan</h2>
        <table class="w-full text-sm">
          <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-1">Plan</th><th class="text-right">Active</th><th class="text-right">MRR</th></tr></thead>
          <tbody>
            @for (p of r.byPlan; track p.plan) {
              <tr class="border-b border-slate-100"><td class="py-2 text-slate-800">{{ p.plan }}</td><td class="text-right text-slate-600">{{ p.activeCount }}</td><td class="text-right font-medium text-slate-800">₹{{ p.mrr | number:'1.0-0' }}</td></tr>
            }
            @if (!r.byPlan.length) { <tr><td colspan="3" class="py-6 text-center text-slate-400">No active subscriptions.</td></tr> }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class SuperAdminRevenueComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly revenue = signal<PlatformRevenue | null>(null);
  ngOnInit(): void { this.svc.revenue().subscribe((r) => this.revenue.set(r)); }
}
