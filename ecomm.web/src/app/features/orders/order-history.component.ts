import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OrderListItem } from '../../core/models/order.model';
import { OrderService } from '../../core/services/order.service';
import { orderStatusClass } from './order-status';

@Component({
  selector: 'app-order-history',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `
    <div class="space-y-4">
      <h2 class="font-semibold text-slate-800">My orders</h2>
      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if (orders().length === 0) {
        <div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-slate-500">
          <!-- Order Now, not /products: the price list is where an order actually gets
               placed, and the catalogue page is not part of the Phase 1 flow. -->
          No orders yet. <a routerLink="/order" class="text-primary hover:underline">Start shopping</a>.
        </div>
      } @else {
        @for (o of orders(); track o.orderId) {
          <a [routerLink]="['/account/orders', o.orderId]" class="flex items-center gap-4 bg-white rounded-xl border border-slate-200 p-4 hover:border-primary transition">
            <img [src]="o.firstItemImage || 'https://placehold.co/64x64?text=%20'" [alt]="o.firstItemName || ''" class="w-14 h-14 object-cover rounded-lg border border-slate-100" />
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <span class="font-medium text-slate-800">{{ o.orderNumber }}</span>
                <span class="text-[11px] px-2 py-0.5 rounded-full" [class]="badge(o.status)">{{ o.status }}</span>
              </div>
              <div class="text-sm text-slate-500 truncate">{{ o.firstItemName }}@if (o.itemCount > 1) { <span class="text-slate-400"> +{{ o.itemCount - 1 }} more</span> }</div>
              <div class="text-xs text-slate-400">{{ (o.placedAt || o.createdAt) | date:'dd MMM yyyy' }}</div>
            </div>
            <div class="text-right font-semibold text-slate-800">{{ o.totalAmount | currency:'INR':'symbol':'1.2-2' }}</div>
          </a>
        }
      }
    </div>
  `,
})
export class OrderHistoryComponent implements OnInit {
  private readonly svc = inject(OrderService);
  readonly orders = signal<OrderListItem[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.svc.listMine().subscribe({
      next: (o) => { this.orders.set(o); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  badge(status: string): string { return orderStatusClass(status); }
}
