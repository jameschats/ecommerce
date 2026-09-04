import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PagedResult } from '../../../core/models/api-response.model';
import { Order, OrderListItem } from '../../../core/models/order.model';
import { OrderService } from '../../../core/services/order.service';
import { orderStatusClass } from '../../orders/order-status';

const FLOW = ['Paid', 'Packed', 'Shipped', 'Delivered'];

@Component({
  selector: 'app-admin-orders',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center justify-between mb-4">
        <h1 class="text-xl font-bold text-slate-900">Orders</h1>
        <select [(ngModel)]="status" (ngModelChange)="reload()" class="input max-w-[180px]">
          <option value="">All statuses</option>
          @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
        </select>
      </div>

      <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
        @if (loading()) { <div class="p-10 text-center text-slate-400">Loading…</div> }
        @else {
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-slate-500 text-left">
              <tr><th class="px-4 py-2">Order</th><th class="px-4 py-2">Date</th><th class="px-4 py-2 w-24">Items</th><th class="px-4 py-2 w-28">Total</th><th class="px-4 py-2 w-28">Status</th></tr>
            </thead>
            <tbody>
              @for (o of result()?.items ?? []; track o.orderId) {
                <tr class="border-t border-slate-100 hover:bg-slate-50 cursor-pointer" (click)="open(o)">
                  <td class="px-4 py-2 text-slate-800">{{ o.orderNumber }}<div class="text-xs text-slate-400">{{ o.firstItemName }}</div></td>
                  <td class="px-4 py-2 text-slate-500">{{ (o.placedAt || o.createdAt) | date:'dd MMM yy' }}</td>
                  <td class="px-4 py-2 text-slate-500">{{ o.itemCount }}</td>
                  <td class="px-4 py-2 text-slate-700">{{ o.totalAmount | currency:'INR':'symbol':'1.2-2' }}</td>
                  <td class="px-4 py-2"><span class="text-[11px] px-2 py-0.5 rounded-full" [class]="badge(o.status)">{{ o.status }}</span></td>
                </tr>
              }
              @if ((result()?.items?.length ?? 0) === 0) { <tr><td colspan="5" class="px-4 py-10 text-center text-slate-400">No orders.</td></tr> }
            </tbody>
          </table>
        }
      </div>
    </div>

    @if (selected(); as o) {
      <div class="fixed inset-0 bg-black/40 z-40 flex items-center justify-center p-4" (click)="selected.set(null)">
        <div class="bg-white rounded-2xl w-full max-w-lg max-h-[85vh] overflow-auto p-5" (click)="$event.stopPropagation()">
          <div class="flex items-center justify-between mb-3">
            <h2 class="font-semibold text-slate-800">{{ o.orderNumber }} <span class="text-[11px] px-2 py-0.5 rounded-full" [class]="badge(o.status)">{{ o.status }}</span></h2>
            <button type="button" (click)="selected.set(null)" class="text-slate-400 hover:text-slate-700 text-xl">×</button>
          </div>
          <div class="text-sm text-slate-600 mb-3">
            @if (o.shippingAddress; as a) { <div>{{ a.recipientName }} · {{ a.phone }}</div><div>{{ a.line1 }}, {{ a.city }}, {{ a.state }} {{ a.pincode }}</div> }
          </div>
          <div class="divide-y divide-slate-100 border-y border-slate-100 mb-3">
            @for (it of o.items; track it.orderItemId) {
              <div class="flex justify-between py-2 text-sm"><span class="text-slate-700">{{ it.productName }} × {{ it.quantity }}</span><span>{{ it.lineTotal | currency:'INR':'symbol':'1.2-2' }}</span></div>
            }
          </div>
          <div class="flex justify-between text-sm font-semibold mb-1"><span>Total</span><span>{{ o.totalAmount | currency:'INR':'symbol':'1.2-2' }}</span></div>
          <div class="text-xs text-slate-400 mb-4">{{ o.paymentMethod }} · {{ o.paymentStatus }}</div>

          @if (o.shipment; as s) {
            <div class="bg-slate-50 border border-slate-200 rounded-lg p-3 text-sm mb-3">
              <div class="font-medium text-slate-700 mb-0.5">Shipment · {{ s.status }}</div>
              <div class="text-slate-600">{{ s.courier }} · {{ s.trackingNumber }}</div>
              @if (s.shippedAt) { <div class="text-xs text-slate-400">Shipped {{ s.shippedAt | date:'dd MMM, HH:mm' }}</div> }
              @if (s.deliveredAt) { <div class="text-xs text-slate-400">Delivered {{ s.deliveredAt | date:'dd MMM, HH:mm' }}</div> }
            </div>
          }

          @if (msg(); as m) { <p class="text-sm text-green-600 mb-2">{{ m }}</p> }
          @if (err(); as e) { <p class="text-sm text-red-600 mb-2">{{ e }}</p> }

          <!-- Ship form (Confirmed/Packed → dispatch with courier + tracking) -->
          @if (o.status === 'Packed' || o.status === 'Confirmed') {
            <div class="border border-slate-200 rounded-lg p-3 mb-3">
              <div class="text-sm font-medium text-slate-700 mb-2">Create shipment (notifies the customer)</div>
              <div class="grid grid-cols-2 gap-2">
                <input [(ngModel)]="shipCourier" name="courier" placeholder="Courier (e.g. Delhivery)" class="input" />
                <input [(ngModel)]="shipTracking" name="tracking" placeholder="Tracking number" class="input" />
                <input type="date" [(ngModel)]="shipEta" name="eta" class="input col-span-2" />
              </div>
              <button type="button" (click)="createShipment(o)" [disabled]="busy() || !shipCourier.trim() || !shipTracking.trim()"
                class="btn-primary text-sm px-4 py-2 mt-2 disabled:opacity-50">Ship &amp; notify</button>
            </div>
          }

          <div class="flex flex-wrap gap-2">
            @if (o.status === 'Paid' || o.status === 'Confirmed') {
              <button type="button" (click)="advance(o, 'Packed')" [disabled]="busy()" class="btn-primary text-sm px-4 py-2">Mark Packed</button>
            }
            @if (o.status === 'Shipped') {
              <button type="button" (click)="markDelivered(o)" [disabled]="busy()" class="btn-primary text-sm px-4 py-2">Mark Delivered</button>
            }
            <button type="button" (click)="invoice(o.orderId)" class="btn-ghost border border-slate-300 text-sm">Invoice PDF</button>
            @if (o.status === 'Paid' || o.status === 'Confirmed' || o.status === 'Packed' || o.status === 'Pending') {
              <button type="button" (click)="cancel(o)" [disabled]="busy()" class="text-sm px-4 py-2 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">{{ o.status === 'Confirmed' ? 'Cancel order' : 'Cancel & refund' }}</button>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminOrdersComponent implements OnInit {
  private readonly svc = inject(OrderService);
  readonly statuses = ['Pending', 'Paid', 'Confirmed', 'Packed', 'Shipped', 'Delivered', 'Cancelled'];

  readonly result = signal<PagedResult<OrderListItem> | null>(null);
  readonly loading = signal(true);
  readonly selected = signal<Order | null>(null);
  readonly busy = signal(false);
  readonly msg = signal<string | null>(null);
  readonly err = signal<string | null>(null);
  status = '';
  shipCourier = '';
  shipTracking = '';
  shipEta = '';

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading.set(true);
    this.svc.adminList(this.status || undefined).subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  open(o: OrderListItem): void {
    this.msg.set(null); this.err.set(null);
    this.shipCourier = ''; this.shipTracking = ''; this.shipEta = '';
    this.svc.adminGet(o.orderId).subscribe((d) => this.selected.set(d));
  }

  createShipment(o: Order): void {
    this.busy.set(true); this.msg.set(null); this.err.set(null);
    this.svc.adminCreateShipment(o.orderId, this.shipCourier.trim(), this.shipTracking.trim(), this.shipEta || null).subscribe({
      next: (d) => { this.selected.set(d); this.busy.set(false); this.msg.set('Shipment created — customer notified.'); this.reload(); },
      error: (e: unknown) => { this.busy.set(false); this.err.set(this.m(e)); },
    });
  }

  markDelivered(o: Order): void {
    this.busy.set(true); this.msg.set(null); this.err.set(null);
    this.svc.adminMarkDelivered(o.orderId).subscribe({
      next: (d) => { this.selected.set(d); this.busy.set(false); this.msg.set('Marked delivered.'); this.reload(); },
      error: (e: unknown) => { this.busy.set(false); this.err.set(this.m(e)); },
    });
  }

  badge(s: string): string { return orderStatusClass(s); }
  nextStatus(s: string): string | null {
    const i = FLOW.indexOf(s);
    return i >= 0 && i < FLOW.length - 1 ? FLOW[i + 1] : null;
  }

  advance(o: Order, to: string): void {
    this.busy.set(true); this.msg.set(null); this.err.set(null);
    this.svc.adminUpdateStatus(o.orderId, to).subscribe({
      next: (d) => { this.selected.set(d); this.busy.set(false); this.msg.set(`Marked ${to}.`); this.reload(); },
      error: (e: unknown) => { this.busy.set(false); this.err.set(this.m(e)); },
    });
  }

  cancel(o: Order): void {
    this.busy.set(true); this.msg.set(null); this.err.set(null);
    this.svc.adminCancel(o.orderId, 'Cancelled by admin').subscribe({
      next: (d) => { this.selected.set(d); this.busy.set(false); this.msg.set('Order cancelled & refunded.'); this.reload(); },
      error: (e: unknown) => { this.busy.set(false); this.err.set(this.m(e)); },
    });
  }

  invoice(id: number): void { this.svc.downloadInvoice(id, true); }

  private m(e: unknown): string {
    return (e as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.';
  }
}
