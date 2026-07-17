import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { PlatformRevenue, TenantSummary } from '../../core/models/superadmin.model';
import { standingClass } from './superadmin-ui';

/** Store directory: revenue strip + searchable table; each row links to the tenant detail page. */
@Component({
  selector: 'app-superadmin-stores',
  imports: [FormsModule, RouterLink],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-4">Stores</h1>

    @if (revenue(); as r) {
      <div class="grid grid-cols-2 sm:grid-cols-5 gap-3 mb-6">
        <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">MRR</div><div class="text-2xl font-bold text-slate-900">₹{{ r.mrr }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">Stores</div><div class="text-2xl font-bold">{{ r.totalTenants }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">Active</div><div class="text-2xl font-bold text-green-600">{{ r.active }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">Trial</div><div class="text-2xl font-bold text-blue-600">{{ r.trial }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">Suspended</div><div class="text-2xl font-bold text-red-500">{{ r.suspended }}</div></div>
      </div>
    }

    <div class="bg-white border border-slate-200 rounded-xl p-4">
      <input [(ngModel)]="search" (ngModelChange)="load()" placeholder="Search stores…" class="input w-full mb-3" />
      <table class="w-full text-sm">
        <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-1">Store</th><th>Plan</th><th>Standing</th><th></th></tr></thead>
        <tbody>
          @for (t of tenants(); track t.tenantId) {
            <tr class="border-b border-slate-100 hover:bg-slate-50">
              <td class="py-2"><a [routerLink]="['/superadmin/tenants', t.tenantId]" class="font-medium text-slate-800 hover:text-blue-600">{{ t.name }}</a><div class="text-xs text-slate-400">{{ t.slug }} · {{ t.userCount }} users · {{ t.orderCount }} orders</div></td>
              <td>{{ t.planName || '—' }}<div class="text-xs text-slate-400">{{ t.subStatus }}</div></td>
              <td><span class="text-xs px-1.5 py-0.5 rounded" [class]="standingClass(t.standing)">{{ t.standing }}</span>@if (t.suspended) { <span class="text-xs text-red-500 block">suspended</span> }</td>
              <td class="text-right"><a [routerLink]="['/superadmin/tenants', t.tenantId]" class="text-blue-600 text-xs">Manage →</a></td>
            </tr>
          }
          @if (!tenants().length) { <tr><td colspan="4" class="py-8 text-center text-slate-400">No stores found.</td></tr> }
        </tbody>
      </table>
    </div>
  `,
})
export class SuperAdminStoresComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly tenants = signal<TenantSummary[]>([]);
  readonly revenue = signal<PlatformRevenue | null>(null);
  readonly standingClass = standingClass;
  search = '';

  ngOnInit(): void {
    this.svc.revenue().subscribe((r) => this.revenue.set(r));
    this.load();
  }
  load(): void { this.svc.tenants(this.search).subscribe((t) => this.tenants.set(t)); }
}
