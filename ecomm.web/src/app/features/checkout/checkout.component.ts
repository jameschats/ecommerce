import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Address } from '../../core/models/account.model';
import { CheckoutQuote, PlaceOrderResult } from '../../core/models/order.model';
import { AccountService } from '../../core/services/account.service';
import { CartService } from '../../core/services/cart.service';
import { OrderService } from '../../core/services/order.service';

type RazorpayWindow = { Razorpay?: new (opts: unknown) => { open: () => void } };

@Component({
  selector: 'app-checkout',
  imports: [RouterLink, CurrencyPipe, FormsModule],
  template: `
    <section class="page-container py-8">
      <h1 class="text-xl font-bold text-slate-900 mb-5">Checkout</h1>

      @if (cartEmpty()) {
        <div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-slate-500">
          Your cart is empty. <a routerLink="/products" class="text-primary hover:underline">Browse products</a>.
        </div>
      } @else {
        <div class="grid lg:grid-cols-3 gap-6 items-start">
          <!-- Left: address + items -->
          <div class="lg:col-span-2 space-y-6">
            <div class="bg-white rounded-xl border border-slate-200 p-5">
              <div class="flex items-center justify-between mb-3">
                <h2 class="font-semibold text-slate-800">Delivery address</h2>
                <a routerLink="/account/addresses" class="text-sm text-primary hover:underline">Manage</a>
              </div>
              @if (loadingAddr()) { <p class="text-slate-400 text-sm">Loading…</p> }
              @else if (addresses().length === 0) {
                <p class="text-slate-500 text-sm">No saved addresses. <a routerLink="/account/addresses" class="text-primary hover:underline">Add one</a> to continue.</p>
              } @else {
                <div class="space-y-2">
                  @for (a of addresses(); track a.customerAddressId) {
                    <label class="flex gap-3 p-3 rounded-lg border cursor-pointer" [class]="selectedId() === a.customerAddressId ? 'border-primary bg-primary/5' : 'border-slate-200'">
                      <input type="radio" name="addr" [checked]="selectedId() === a.customerAddressId" (change)="selectAddress(a.customerAddressId)" class="mt-1" />
                      <div class="text-sm">
                        <div class="font-medium text-slate-800">{{ a.label || a.addressType }} @if (a.isDefault) { <span class="text-[10px] text-primary">· default</span> }</div>
                        <div class="text-slate-500">{{ a.line1 }}@if (a.line2) {, {{ a.line2 }}}, {{ a.city }}, {{ a.state }} {{ a.pincode }}</div>
                      </div>
                    </label>
                  }
                </div>
              }
            </div>

            @if (quote(); as q) {
              <div class="bg-white rounded-xl border border-slate-200 p-5">
                <h2 class="font-semibold text-slate-800 mb-3">Items</h2>
                <div class="divide-y divide-slate-100">
                  @for (l of q.lines; track l.productId + '-' + (l.productVariantId || 0)) {
                    <div class="flex justify-between py-2 text-sm">
                      <span class="text-slate-700">{{ l.name }}@if (l.variantLabel) { <span class="text-slate-400"> · {{ l.variantLabel }}</span> } <span class="text-slate-400">× {{ l.quantity }}</span></span>
                      <span class="text-slate-700">{{ l.lineSubtotal | currency:'INR':'symbol':'1.0-0' }}</span>
                    </div>
                  }
                </div>
              </div>
            }
          </div>

          <!-- Right: summary -->
          <div class="bg-white rounded-xl border border-slate-200 p-5 lg:sticky lg:top-20">
            <h2 class="font-semibold text-slate-800 mb-3">Order summary</h2>
            @if (loadingQuote()) { <p class="text-slate-400 text-sm">Calculating…</p> }
            @else if (quote(); as q) {
              <div class="space-y-1 text-sm">
                <div class="flex justify-between text-slate-600"><span>Subtotal</span><span>{{ q.subtotal | currency:'INR':'symbol':'1.2-2' }}</span></div>
                @if (q.taxMode === 'Exclusive') {
                  @if (q.interState) {
                    <div class="flex justify-between text-slate-600"><span>IGST</span><span>{{ q.igst | currency:'INR':'symbol':'1.2-2' }}</span></div>
                  } @else {
                    <div class="flex justify-between text-slate-600"><span>CGST</span><span>{{ q.cgst | currency:'INR':'symbol':'1.2-2' }}</span></div>
                    <div class="flex justify-between text-slate-600"><span>SGST</span><span>{{ q.sgst | currency:'INR':'symbol':'1.2-2' }}</span></div>
                  }
                } @else if (q.taxMode === 'Inclusive') {
                  <div class="flex justify-between text-slate-400 text-xs"><span>Inclusive of all taxes</span><span></span></div>
                }
                <div class="flex justify-between text-slate-600"><span>Shipping @if (q.estimatedDays) { <span class="text-slate-400 text-xs">({{ q.estimatedDays }}d)</span> }</span><span>{{ q.shippingCharge === 0 ? 'Free' : (q.shippingCharge | currency:'INR':'symbol':'1.2-2') }}</span></div>
                @if (q.discountAmount > 0) {
                  <div class="flex justify-between text-green-700"><span>Discount @if (q.couponCode) { <span class="text-xs">({{ q.couponCode }})</span> }</span><span>−{{ q.discountAmount | currency:'INR':'symbol':'1.2-2' }}</span></div>
                }
                <div class="border-t border-slate-100 pt-2 mt-1 flex justify-between font-bold text-slate-900"><span>Total</span><span>{{ q.total | currency:'INR':'symbol':'1.2-2' }}</span></div>
              </div>

              <!-- Coupon -->
              <div class="mt-3">
                @if (appliedCoupon() && q.couponApplied) {
                  <div class="flex items-center justify-between text-sm bg-green-50 border border-green-200 rounded-lg px-3 py-2">
                    <span class="text-green-700 font-medium">{{ q.couponCode }} applied</span>
                    <button type="button" (click)="removeCoupon()" class="text-slate-500 hover:text-red-600 text-xs">Remove</button>
                  </div>
                } @else {
                  <div class="flex gap-2">
                    <input [ngModel]="couponInput()" (ngModelChange)="couponInput.set($event)" (keyup.enter)="applyCoupon()"
                      placeholder="Coupon code" class="input flex-1 uppercase" />
                    <button type="button" (click)="applyCoupon()" [disabled]="!couponInput().trim() || loadingQuote()" class="px-3 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50 disabled:opacity-50">Apply</button>
                  </div>
                }
                @if (appliedCoupon() && !q.couponApplied && q.couponMessage) {
                  <p class="text-xs text-red-600 mt-1">{{ q.couponMessage }}</p>
                }
              </div>

              @if (!q.serviceable && selectedId()) { <p class="text-sm text-red-600 mt-3">{{ q.message }}</p> }

              <button type="button" (click)="placeOrder()" [disabled]="!canPlace()" class="btn-primary w-full mt-4 py-3 disabled:opacity-50 disabled:cursor-not-allowed">
                {{ processing() ? 'Processing…' : 'Place order & pay' }}
              </button>
              <p class="text-xs text-slate-400 mt-2 text-center">Payments via {{ gatewayLabel() }}</p>
            }
            @if (error(); as e) { <p class="text-sm text-red-600 mt-2 text-center">{{ e }}</p> }
          </div>
        </div>
      }
    </section>
  `,
})
export class CheckoutComponent implements OnInit {
  private readonly orders = inject(OrderService);
  private readonly account = inject(AccountService);
  private readonly cart = inject(CartService);
  private readonly router = inject(Router);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly addresses = signal<Address[]>([]);
  readonly selectedId = signal<number | null>(null);
  readonly quote = signal<CheckoutQuote | null>(null);
  readonly loadingAddr = signal(true);
  readonly loadingQuote = signal(false);
  readonly processing = signal(false);
  readonly error = signal<string | null>(null);
  readonly couponInput = signal('');
  readonly appliedCoupon = signal<string | null>(null);

  readonly cartEmpty = computed(() => this.cart.itemCount() === 0 && !this.processing());
  readonly canPlace = computed(() =>
    !this.processing() && !!this.selectedId() && !!this.quote()?.serviceable && (this.quote()?.lines.length ?? 0) > 0);

  ngOnInit(): void {
    this.account.listAddresses().subscribe({
      next: (list) => {
        this.addresses.set(list);
        const def = list.find((a) => a.isDefault) ?? list[0];
        this.loadingAddr.set(false);
        if (def) { this.selectedId.set(def.customerAddressId); this.loadQuote(def.customerAddressId); }
        else this.loadQuote(null);
      },
      error: () => this.loadingAddr.set(false),
    });
  }

  selectAddress(id: number): void {
    this.selectedId.set(id);
    this.loadQuote(id);
  }

  gatewayLabel(): string {
    return this.quote()?.serviceable ? 'Razorpay / secure checkout' : 'Razorpay';
  }

  private loadQuote(addressId: number | null): void {
    this.loadingQuote.set(true);
    this.error.set(null);
    this.orders.quote(addressId, this.appliedCoupon()).subscribe({
      next: (q) => { this.quote.set(q); this.loadingQuote.set(false); },
      error: () => { this.loadingQuote.set(false); this.error.set('Could not load the order summary.'); },
    });
  }

  applyCoupon(): void {
    const code = this.couponInput().trim().toUpperCase();
    if (!code) return;
    this.appliedCoupon.set(code);
    this.loadQuote(this.selectedId());
  }

  removeCoupon(): void {
    this.appliedCoupon.set(null);
    this.couponInput.set('');
    this.loadQuote(this.selectedId());
  }

  placeOrder(): void {
    const addressId = this.selectedId();
    if (!addressId) return;
    this.processing.set(true);
    this.error.set(null);
    // Only send the coupon if the server confirmed it applies, so an invalid code can't block checkout.
    const coupon = this.quote()?.couponApplied ? this.appliedCoupon() : null;
    this.orders.place(addressId, null, null, coupon).subscribe({
      next: (res) => this.pay(res),
      error: (e: unknown) => {
        this.processing.set(false);
        this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not place the order.');
      },
    });
  }

  private pay(res: PlaceOrderResult): void {
    // Mock gateway: no public key → confirm immediately.
    if (!res.payment.publicKey) {
      this.confirm(res.orderId, `mock_pay_${res.payment.gatewayOrderId}`, 'mock_signature');
      return;
    }
    if (!this.isBrowser) return;
    this.openRazorpay(res);
  }

  private openRazorpay(res: PlaceOrderResult): void {
    const w = window as unknown as RazorpayWindow;
    const launch = () => {
      const rzp = new w.Razorpay!({
        key: res.payment.publicKey,
        order_id: res.payment.gatewayOrderId,
        amount: Math.round(res.payment.amount * 100),
        currency: res.payment.currency,
        name: 'CalendarShop',
        description: res.orderNumber,
        handler: (r: { razorpay_payment_id: string; razorpay_signature: string }) =>
          this.confirm(res.orderId, r.razorpay_payment_id, r.razorpay_signature),
        modal: { ondismiss: () => { this.processing.set(false); this.error.set('Payment cancelled.'); } },
      });
      rzp.open();
    };
    if (w.Razorpay) { launch(); return; }
    const s = document.createElement('script');
    s.src = 'https://checkout.razorpay.com/v1/checkout.js';
    s.onload = launch;
    s.onerror = () => { this.processing.set(false); this.error.set('Could not load the payment widget.'); };
    document.body.appendChild(s);
  }

  private confirm(orderId: number, paymentId: string, signature: string): void {
    this.orders.confirm(orderId, paymentId, signature).subscribe({
      next: () => {
        this.cart.reload();
        this.router.navigate(['/account/orders', orderId], { queryParams: { placed: 1 } });
      },
      error: (e: unknown) => {
        this.processing.set(false);
        this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Payment could not be confirmed.');
      },
    });
  }
}
