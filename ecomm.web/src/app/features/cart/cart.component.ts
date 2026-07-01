import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { Cart, CartItem } from '../../core/models/cart.model';
import { AuthService } from '../../core/services/auth.service';
import { CartService } from '../../core/services/cart.service';

@Component({
  selector: 'app-cart',
  imports: [RouterLink, CurrencyPipe],
  template: `
    <section class="page-container py-8">
      <h1 class="text-xl font-bold text-slate-900 mb-5">Your cart</h1>

      @if (items().length === 0) {
        <div class="text-center py-20 bg-white rounded-xl border border-slate-200">
          <div class="mx-auto w-16 h-16 rounded-full bg-slate-100 flex items-center justify-center text-slate-400 mb-4">
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/><path d="M1 1h4l2.7 12.4a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.6L23 6H6"/></svg>
          </div>
          <p class="text-slate-500">Your cart is empty.</p>
          <a routerLink="/products" class="inline-block mt-5 btn-primary px-5 py-2.5">Continue shopping</a>
        </div>
      } @else {
        <div class="grid lg:grid-cols-3 gap-6 items-start">
          <!-- Items -->
          <div class="lg:col-span-2 bg-white rounded-xl border border-slate-200 divide-y divide-slate-100">
            @for (it of items(); track it.cartItemId) {
              <div class="flex gap-4 p-4">
                <a [routerLink]="['/product', it.slug]" class="shrink-0">
                  <img [src]="it.imageUrl || 'https://placehold.co/96x96?text=No+image'" [alt]="it.name" class="w-20 h-20 object-cover rounded-lg border border-slate-100" />
                </a>
                <div class="flex-1 min-w-0">
                  <a [routerLink]="['/product', it.slug]" class="font-medium text-slate-800 hover:text-primary line-clamp-2">{{ it.name }}</a>
                  @if (it.variantLabel) { <div class="text-xs text-slate-400 mt-0.5">{{ it.variantLabel }}</div> }
                  <div class="text-sm text-slate-500 mt-0.5">{{ it.unitPrice | currency:'INR':'symbol':'1.0-0' }}</div>
                  @if (!it.inStock) { <div class="text-xs text-red-600 mt-1">Out of stock</div> }
                  @else if (it.quantity > it.availableQty) { <div class="text-xs text-orange-600 mt-1">Only {{ it.availableQty }} available</div> }

                  <div class="flex items-center gap-4 mt-2">
                    <div class="flex items-center border border-slate-300 rounded-lg">
                      <button type="button" (click)="dec(it)" [disabled]="busy()" class="w-8 h-8 text-slate-600 hover:bg-slate-50 disabled:opacity-40">−</button>
                      <span class="w-9 text-center text-sm">{{ it.quantity }}</span>
                      <button type="button" (click)="inc(it)" [disabled]="busy() || it.quantity >= it.availableQty" class="w-8 h-8 text-slate-600 hover:bg-slate-50 disabled:opacity-40">+</button>
                    </div>
                    <button type="button" (click)="remove(it)" [disabled]="busy()" class="text-sm text-slate-500 hover:text-red-600">Remove</button>
                  </div>
                </div>
                <div class="text-right font-semibold text-slate-800 whitespace-nowrap">{{ it.lineTotal | currency:'INR':'symbol':'1.0-0' }}</div>
              </div>
            }
          </div>

          <!-- Summary -->
          <div class="bg-white rounded-xl border border-slate-200 p-5 lg:sticky lg:top-20">
            <h2 class="font-semibold text-slate-800 mb-3">Order summary</h2>
            <div class="flex justify-between text-sm text-slate-600 mb-1"><span>Subtotal ({{ count() }} items)</span><span>{{ subtotal() | currency:'INR':'symbol':'1.0-0' }}</span></div>
            <div class="flex justify-between text-sm text-slate-500 mb-3"><span>Shipping</span><span>Calculated at checkout</span></div>
            <div class="border-t border-slate-100 pt-3 flex justify-between font-bold text-slate-900"><span>Total</span><span>{{ subtotal() | currency:'INR':'symbol':'1.0-0' }}</span></div>
            @if (taxMode() === 'Inclusive') { <p class="text-xs text-slate-400 mt-1">Inclusive of all taxes</p> }
            <button type="button" (click)="checkout()" [disabled]="!canCheckout()" class="btn-primary w-full mt-4 py-3 disabled:opacity-50 disabled:cursor-not-allowed">Proceed to checkout</button>
            @if (checkoutNote()) { <p class="text-xs text-amber-600 mt-2 text-center">{{ checkoutNote() }}</p> }
            <a routerLink="/products" class="block text-center text-sm text-primary hover:underline mt-3">Continue shopping</a>
            @if (error(); as e) { <p class="text-xs text-red-600 mt-2 text-center">{{ e }}</p> }
          </div>
        </div>
      }
    </section>
  `,
})
export class CartComponent {
  private readonly cartService = inject(CartService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly items = this.cartService.items;
  readonly subtotal = this.cartService.subtotal;
  readonly count = this.cartService.itemCount;
  readonly taxMode = computed(() => this.cartService.cart()?.taxMode ?? 'Exclusive');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly checkoutNote = signal<string | null>(null);

  readonly canCheckout = computed(() =>
    !this.busy() && this.items().length > 0 && this.items().every((i) => i.inStock && i.quantity <= i.availableQty));

  inc(it: CartItem): void {
    if (it.quantity >= it.availableQty) return;
    this.run(this.cartService.updateQty(it.cartItemId, it.quantity + 1));
  }
  dec(it: CartItem): void {
    this.run(this.cartService.updateQty(it.cartItemId, it.quantity - 1));
  }
  remove(it: CartItem): void {
    this.run(this.cartService.remove(it.cartItemId));
  }

  checkout(): void {
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/checkout' } });
      return;
    }
    this.router.navigateByUrl('/checkout');
  }

  private run(obs: Observable<Cart>): void {
    this.busy.set(true);
    this.error.set(null);
    obs.subscribe({
      next: () => this.busy.set(false),
      error: (e: unknown) => {
        this.busy.set(false);
        const msg = (e as { error?: { message?: string } })?.error?.message;
        this.error.set(msg ?? 'Could not update the cart.');
        this.cartService.reload();
      },
    });
  }
}
