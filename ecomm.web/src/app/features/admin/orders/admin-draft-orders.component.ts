import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DraftOrderService, DraftOrderListItem } from '../../../core/services/draft-order.service';

@Component({
  selector: 'app-admin-draft-orders',
  imports: [RouterLink, DecimalPipe, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Draft orders</h1>
        <a routerLink="/admin/draft-orders/new" class="btn-primary">+ Create order</a>
      </div>
      <p class="text-sm text-slate-500 mb-4">Manually create orders for phone or in-person sales, then convert them to real orders.</p>

      <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-slate-50 text-slate-500 text-left">
            <tr><th class="px-4 py-2 font-medium">Draft</th><th class="px-4 py-2 font-medium">Customer</th><th class="px-4 py-2 font-medium text-right">Total</th><th class="px-4 py-2 font-medium">Created</th></tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            @for (d of drafts(); track d.orderId) {
              <tr class="hover:bg-slate-50">
                <td class="px-4 py-3"><a [routerLink]="['/admin/draft-orders', d.orderId]" class="font-medium text-blue-600 hover:underline">{{ d.orderNumber }}</a></td>
                <td class="px-4 py-3 text-slate-600">{{ d.customerName || '—' }}</td>
                <td class="px-4 py-3 text-right font-medium text-slate-800">₹{{ d.totalAmount | number:'1.0-2' }}</td>
                <td class="px-4 py-3 text-slate-500">{{ d.createdAt | date:'mediumDate' }}</td>
              </tr>
            }
            @if (!loading() && !drafts().length) { <tr><td colspan="4" class="px-4 py-10 text-center text-slate-400">No draft orders.</td></tr> }
          </tbody>
        </table>
        @if (loading()) { <div class="p-6 text-center text-slate-400 text-sm">Loading…</div> }
      </div>
    </div>
  `,
})
export class AdminDraftOrdersComponent implements OnInit {
  private readonly api = inject(DraftOrderService);
  readonly drafts = signal<DraftOrderListItem[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.api.list().subscribe({ next: (d) => { this.drafts.set(d); this.loading.set(false); }, error: () => this.loading.set(false) });
  }
}
