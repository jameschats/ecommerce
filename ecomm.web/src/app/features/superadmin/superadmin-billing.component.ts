import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { BillingCharge, SubStatusRow } from '../../core/models/superadmin.model';

/** Billing oversight: subscriptions by status (dunning attention) + recent platform charges. */
@Component({
  selector: 'app-superadmin-billing',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Billing</h1>
    <p class="text-sm text-slate-500 mb-4">Subscriptions needing attention and the platform's recent charges.</p>

    <div class="grid lg:grid-cols-2 gap-4">
      <div class="bg-white border border-slate-200 rounded-xl p-4">
        <div class="flex items-center justify-between mb-3">
          <h2 class="font-semibold text-slate-800">Subscriptions</h2>
          <select [(ngModel)]="status" (ngModelChange)="loadSubs()" class="input py-1 text-sm w-40">
            <option value="">All</option><option>Trial</option><option>Active</option><option>PastDue</option><option>Suspended</option><option>Cancelled</option>
          </select>
        </div>
        <table class="w-full text-sm">
          <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-1">Store</th><th>Plan</th><th>Status</th><th>Renews</th></tr></thead>
          <tbody>
            @for (s of subs(); track s.tenantId) {
              <tr class="border-b border-slate-100">
                <td class="py-2"><a [routerLink]="['/superadmin/tenants', s.tenantId]" class="text-slate-800 hover:text-blue-600">{{ s.name }}</a></td>
                <td class="text-slate-500">{{ s.planName || '—' }}</td>
                <td><span class="text-xs px-1.5 py-0.5 rounded" [class]="statusClass(s.status)">{{ s.status }}</span></td>
                <td class="text-slate-500 text-xs">{{ s.currentPeriodEnd ? (s.currentPeriodEnd | date:'mediumDate') : '—' }}@if (s.graceEndsAt) { <span class="text-amber-600 block">grace {{ s.graceEndsAt | date:'dd MMM' }}</span> }</td>
              </tr>
            }
            @if (!subs().length) { <tr><td colspan="4" class="py-6 text-center text-slate-400">None.</td></tr> }
          </tbody>
        </table>
      </div>

      <div class="bg-white border border-slate-200 rounded-xl p-4">
        <h2 class="font-semibold text-slate-800 mb-3">Recent charges</h2>
        <table class="w-full text-sm">
          <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-1">When</th><th>Store</th><th class="text-right">Amount</th><th>Status</th></tr></thead>
          <tbody>
            @for (c of charges(); track c.id) {
              <tr class="border-b border-slate-100">
                <td class="py-2 text-slate-500 text-xs whitespace-nowrap">{{ c.billedAt | date:'short' }}</td>
                <td><a [routerLink]="['/superadmin/tenants', c.tenantId]" class="text-blue-600 hover:underline">#{{ c.tenantId }}</a></td>
                <td class="text-right font-medium text-slate-800">₹{{ c.amount | number:'1.0-0' }}</td>
                <td><span class="text-xs text-green-600">{{ c.status }}</span></td>
              </tr>
            }
            @if (!charges().length) { <tr><td colspan="4" class="py-6 text-center text-slate-400">No charges yet.</td></tr> }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class SuperAdminBillingComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly subs = signal<SubStatusRow[]>([]);
  readonly charges = signal<BillingCharge[]>([]);
  status = '';

  ngOnInit(): void { this.loadSubs(); this.svc.charges(100).subscribe((c) => this.charges.set(c)); }
  loadSubs(): void { this.svc.subscriptions(this.status).subscribe((s) => this.subs.set(s)); }

  statusClass(s: string): string {
    return s === 'PastDue' ? 'bg-amber-50 text-amber-700 border border-amber-200'
      : s === 'Suspended' || s === 'Cancelled' ? 'bg-red-50 text-red-700 border border-red-200'
      : s === 'Active' ? 'bg-green-50 text-green-700 border border-green-200'
      : 'bg-blue-50 text-blue-700 border border-blue-200';
  }
}
