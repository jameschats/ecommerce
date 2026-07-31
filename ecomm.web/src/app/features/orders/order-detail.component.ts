import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Order } from '../../core/models/order.model';
import { ConversationService } from '../../core/services/conversation.service';
import { OrderService } from '../../core/services/order.service';
import { orderStatusClass } from './order-status';

@Component({
  selector: 'app-order-detail',
  imports: [RouterLink, CurrencyPipe, DatePipe, FormsModule],
  template: `
    @if (loading()) {
      <p class="text-slate-400 text-sm">Loading…</p>
    } @else if (order(); as o) {
      <div class="space-y-5">
        @if (justPlaced()) {
          <div class="bg-green-50 border border-green-200 rounded-xl p-4 flex items-center gap-3">
            <span class="text-2xl">✅</span>
            <div><div class="font-semibold text-green-800">Order placed!</div><div class="text-sm text-green-700">Thank you — your order {{ o.orderNumber }} is confirmed.</div></div>
          </div>
        }

        <div class="flex items-center justify-between">
          <div>
            <a routerLink="/account/orders" class="text-sm text-primary hover:underline">← All orders</a>
            <h2 class="font-semibold text-slate-800 mt-1">{{ o.orderNumber }}
              <span class="text-[11px] px-2 py-0.5 rounded-full align-middle" [class]="badge(o.status)">{{ o.status }}</span>
            </h2>
            <div class="text-xs text-slate-400">Placed {{ (o.placedAt || o.createdAt) | date:'dd MMM yyyy, HH:mm' }}</div>
          </div>
          <div class="flex gap-2">
            @if (o.invoiceNumber) { <button type="button" (click)="invoice(o.orderId)" class="btn-ghost border border-slate-300 text-sm">Invoice PDF</button> }
            <button type="button" (click)="askOpen.set(!askOpen())" class="btn-ghost border border-slate-300 text-sm">Ask about this order</button>
            @if (o.canCancel) { <button type="button" (click)="cancel(o.orderId)" [disabled]="busy()" class="text-sm px-3 py-2 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Cancel order</button> }
          </div>
        </div>

        @if (askOpen()) {
          <div class="mb-5 bg-white rounded-xl border border-slate-200 p-4">
            @if (askSent()) {
              <p class="text-sm text-green-700">Message sent — we'll reply by email, and you can follow it in
                <a routerLink="/account/conversations" class="underline">your messages</a>.</p>
            } @else {
              <label class="lbl">Ask about this order</label>
              <textarea [(ngModel)]="askBody" name="askBody" rows="3" class="input" placeholder="What would you like to know?"></textarea>
              <button type="button" (click)="ask(o.orderId, o.orderNumber)" [disabled]="asking() || !askBody.trim()"
                      class="btn-primary mt-2 disabled:opacity-60">{{ asking() ? 'Sending…' : 'Send' }}</button>
            }
          </div>
        }

        <div class="grid md:grid-cols-3 gap-5 items-start">
          <div class="md:col-span-2 bg-white rounded-xl border border-slate-200 divide-y divide-slate-100">
            @for (it of o.items; track it.orderItemId) {
              <div class="flex justify-between p-4 text-sm">
                <div>
                  <div class="text-slate-800">{{ it.productName }}@if (it.variantLabel) { <span class="text-slate-400"> · {{ it.variantLabel }}</span> }
                    @if (it.isFreeGift) { <span class="text-[10px] bg-green-50 text-green-700 border border-green-200 px-1.5 py-0.5 rounded ml-1 align-middle">FREE GIFT</span> }</div>
                  <div class="text-xs text-slate-400">{{ it.sku }} · GST {{ it.taxRate }}% · × {{ it.quantity }}</div>
                </div>
                <div class="text-right text-slate-700">{{ it.isFreeGift ? 'Free' : (it.lineTotal | currency:'INR':'symbol':'1.2-2') }}</div>
              </div>
            }
          </div>

          <div class="space-y-4">
            <div class="bg-white rounded-xl border border-slate-200 p-4 text-sm">
              <h3 class="font-medium text-slate-700 mb-2">Payment summary</h3>
              <div class="flex justify-between text-slate-600"><span>Subtotal</span><span>{{ o.subtotal | currency:'INR':'symbol':'1.2-2' }}</span></div>
              <div class="flex justify-between text-slate-600"><span>Tax</span><span>{{ o.taxAmount | currency:'INR':'symbol':'1.2-2' }}</span></div>
              <div class="flex justify-between text-slate-600"><span>Shipping</span><span>{{ o.shippingAmount === 0 ? 'Free' : (o.shippingAmount | currency:'INR':'symbol':'1.2-2') }}</span></div>
              <div class="border-t border-slate-100 pt-2 mt-1 flex justify-between font-bold text-slate-900"><span>Total</span><span>{{ o.totalAmount | currency:'INR':'symbol':'1.2-2' }}</span></div>
              <div class="mt-2 text-xs text-slate-400">{{ o.paymentMethod }} · <span [class]="payClass(o.paymentStatus)">{{ o.paymentStatus }}</span></div>
            </div>

            @if (o.shipment; as s) {
              <div class="bg-white rounded-xl border border-slate-200 p-4 text-sm">
                <h3 class="font-medium text-slate-700 mb-1">Tracking</h3>
                <div class="text-slate-600">
                  <div>Status: <span class="font-medium text-slate-800">{{ s.status }}</span></div>
                  @if (s.courier) { <div>Courier: {{ s.courier }}</div> }
                  @if (s.trackingNumber) { <div>Tracking #: <span class="font-medium">{{ s.trackingNumber }}</span></div> }
                  @if (s.estimatedDeliveryDate) { <div class="text-slate-500">Est. delivery: {{ s.estimatedDeliveryDate | date:'dd MMM yyyy' }}</div> }
                  @if (s.shippedAt) { <div class="text-xs text-slate-400 mt-1">Shipped {{ s.shippedAt | date:'dd MMM yyyy' }}</div> }
                  @if (s.deliveredAt) { <div class="text-xs text-slate-400">Delivered {{ s.deliveredAt | date:'dd MMM yyyy' }}</div> }
                </div>
              </div>
            }

            @if (o.shippingAddress; as a) {
              <div class="bg-white rounded-xl border border-slate-200 p-4 text-sm">
                <h3 class="font-medium text-slate-700 mb-1">Deliver to</h3>
                <div class="text-slate-600">@if (a.recipientName) { <div>{{ a.recipientName }}@if (a.phone) { · {{ a.phone }} }</div> }
                  <div>{{ a.line1 }}@if (a.line2) {, {{ a.line2 }}}</div>
                  <div>{{ a.city }}, {{ a.state }} {{ a.pincode }}</div>
                </div>
              </div>
            }
          </div>
        </div>
        @if (error(); as e) { <p class="text-sm text-red-600">{{ e }}</p> }
      </div>
    } @else {
      <div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-slate-500">Order not found.</div>
    }
  `,
})
export class OrderDetailComponent implements OnInit {
  private readonly svc = inject(OrderService);
  private readonly route = inject(ActivatedRoute);

  readonly order = signal<Order | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);

  private readonly convos = inject(ConversationService);
  readonly askOpen = signal(false);
  readonly asking = signal(false);
  readonly askSent = signal(false);
  askBody = '';

  /** Starts a conversation already linked to this order, so the merchant has the context. */
  ask(orderId: number, orderNumber: string): void {
    const body = this.askBody.trim();
    if (!body) return;
    this.asking.set(true);
    this.convos.start({ subject: `Question about order ${orderNumber}`, message: body, orderId }).subscribe({
      next: () => { this.askBody = ''; this.askSent.set(true); this.asking.set(false); },
      error: () => this.asking.set(false),
    });
  }
  readonly error = signal<string | null>(null);
  readonly justPlaced = signal(false);

  ngOnInit(): void {
    this.justPlaced.set(this.route.snapshot.queryParamMap.get('placed') === '1');
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.svc.get(id).subscribe({
      next: (o) => { this.order.set(o); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  badge(s: string): string { return orderStatusClass(s); }
  payClass(s: string | null): string { return s === 'Success' ? 'text-green-600' : s === 'Refunded' ? 'text-purple-600' : 'text-slate-500'; }

  invoice(id: number): void { this.svc.downloadInvoice(id); }

  cancel(id: number): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.svc.cancel(id, 'Cancelled by customer').subscribe({
      next: (o) => { this.order.set(o); this.busy.set(false); this.justPlaced.set(false); },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not cancel the order.');
      },
    });
  }
}
