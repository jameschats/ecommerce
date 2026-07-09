import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DraftOrder, DraftOrderService } from '../../../core/services/draft-order.service';

@Component({
  selector: 'app-admin-draft-order-detail',
  imports: [RouterLink, DecimalPipe],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <a routerLink="/admin/draft-orders" class="text-sm text-slate-500 hover:text-slate-800">← Draft orders</a>

      @if (loading()) { <p class="text-slate-400 text-sm mt-4">Loading…</p> }
      @else if (d(); as order) {
        <div class="flex items-center justify-between mt-2 mb-5">
          <h1 class="text-xl font-bold text-slate-900">{{ order.orderNumber }} <span class="text-xs text-amber-600 align-middle">draft</span></h1>
          <button type="button" (click)="remove()" class="text-sm text-red-500 hover:underline">Delete draft</button>
        </div>
        @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

        <div class="bg-white border border-slate-200 rounded-xl p-5 mb-5">
          <h2 class="font-semibold text-slate-800 mb-1">Customer</h2>
          <p class="text-sm text-slate-600">{{ order.customerName || 'No name' }}{{ order.customerEmail ? ' · ' + order.customerEmail : '' }}</p>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-5 mb-5">
          <h2 class="font-semibold text-slate-800 mb-3">Items</h2>
          @for (l of order.lines; track l.productId) {
            <div class="flex justify-between text-sm py-1.5 border-b border-slate-50 last:border-0">
              <span class="text-slate-700">{{ l.name }} × {{ l.quantity }}</span>
              <span class="font-medium text-slate-800">₹{{ l.lineTotal | number:'1.0-2' }}</span>
            </div>
          }
          <div class="mt-4 space-y-1 text-sm">
            <div class="flex justify-between text-slate-500"><span>Subtotal</span><span>₹{{ order.subtotal | number:'1.0-2' }}</span></div>
            @if (order.discountAmount > 0) { <div class="flex justify-between text-green-600"><span>Discount{{ order.couponCode ? ' (' + order.couponCode + ')' : '' }}</span><span>−₹{{ order.discountAmount | number:'1.0-2' }}</span></div> }
            <div class="flex justify-between text-slate-500"><span>Tax</span><span>₹{{ order.taxAmount | number:'1.0-2' }}</span></div>
            <div class="flex justify-between text-slate-500"><span>Shipping</span><span>₹{{ order.shippingAmount | number:'1.0-2' }}</span></div>
            <div class="flex justify-between font-bold text-slate-900 pt-1 border-t border-slate-100 mt-1"><span>Total</span><span>₹{{ order.totalAmount | number:'1.0-2' }}</span></div>
          </div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-5">
          <h2 class="font-semibold text-slate-800 mb-1">Create the order</h2>
          <p class="text-xs text-slate-500 mb-3">Converting reserves stock, records the payment, and generates an invoice.</p>
          <div class="flex flex-wrap gap-3">
            <button type="button" (click)="convert('Manual')" [disabled]="converting()" class="btn-primary">Mark as paid & create</button>
            <button type="button" (click)="convert('COD')" [disabled]="converting()" class="px-4 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50">Create as Cash on Delivery</button>
          </div>
        </div>
      } @else { <p class="text-slate-400 text-sm mt-4">Draft not found.</p> }
    </div>
  `,
})
export class AdminDraftOrderDetailComponent implements OnInit {
  private readonly api = inject(DraftOrderService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly d = signal<DraftOrder | null>(null);
  readonly loading = signal(true);
  readonly converting = signal(false);
  readonly error = signal<string | null>(null);
  private id = 0;

  ngOnInit(): void {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.get(this.id).subscribe({ next: (o) => { this.d.set(o); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  convert(method: string): void {
    this.converting.set(true); this.error.set(null);
    this.api.convert(this.id, method).subscribe({
      next: (r) => this.router.navigate(['/admin/orders'], { queryParams: { placed: r.orderId } }),
      error: (e: unknown) => { this.converting.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not create the order.'); },
    });
  }

  remove(): void {
    if (!confirm('Delete this draft?')) return;
    this.api.remove(this.id).subscribe({ next: () => this.router.navigateByUrl('/admin/draft-orders') });
  }
}
