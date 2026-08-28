import { CurrencyPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { CartItem } from '../../core/models/cart.model';
import { CartService } from '../../core/services/cart.service';

/** Slide-out mini-cart. Opened by the header cart icon and after add-to-cart; edit qty / remove
 * inline, then jump to checkout — no full-page nav to /cart needed. Rendered once at the app root. */
@Component({
  selector: 'app-cart-drawer',
  imports: [CurrencyPipe, RouterLink],
  template: `
    @if (cart.drawerOpen()) {
      <div class="fixed inset-0 z-[60]">
        <div class="absolute inset-0 bg-black/40" (click)="cart.closeDrawer()"></div>
        <aside class="absolute right-0 top-0 h-full w-full max-w-sm bg-white shadow-xl flex flex-col">
          <div class="flex items-center justify-between px-4 h-14 border-b border-slate-200">
            <h2 class="font-semibold text-slate-800">Your cart ({{ cart.itemCount() }})</h2>
            <button type="button" (click)="cart.closeDrawer()" class="text-slate-400 text-2xl leading-none px-1">×</button>
          </div>

          @if (cart.items().length === 0) {
            <div class="flex-1 flex flex-col items-center justify-center text-center px-6 text-slate-400">
              <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" class="mb-3"><circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/><path d="M1 1h4l2.7 12.4a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.6L23 6H6"/></svg>
              <p class="text-sm">Your cart is empty.</p>
              <button type="button" (click)="cart.closeDrawer()" class="mt-4 text-sm text-primary font-medium">Continue shopping</button>
            </div>
          } @else {
            <div class="flex-1 overflow-auto divide-y divide-slate-100">
              @for (i of cart.items(); track i.cartItemId) {
                <div class="flex gap-3 p-4">
                  <a [routerLink]="['/product', i.slug]" (click)="cart.closeDrawer()" class="w-16 h-16 shrink-0 bg-slate-50 rounded-lg overflow-hidden">
                    @if (i.imageUrl) { <img [src]="i.imageUrl" [alt]="i.name" class="w-full h-full object-cover" /> }
                  </a>
                  <div class="flex-1 min-w-0">
                    <a [routerLink]="['/product', i.slug]" (click)="cart.closeDrawer()" class="text-sm font-medium text-slate-800 line-clamp-2 hover:text-primary">{{ i.name }}</a>
                    @if (i.variantLabel) { <p class="text-xs text-slate-400">{{ i.variantLabel }}</p> }
                    <div class="mt-1.5 flex items-center justify-between gap-2">
                      <div class="flex items-center border border-slate-300 rounded-lg text-sm">
                        <button type="button" (click)="dec(i)" [disabled]="busy()" class="w-7 h-7 text-slate-600 hover:bg-slate-50 disabled:opacity-40">−</button>
                        <span class="w-7 text-center">{{ i.quantity }}</span>
                        <button type="button" (click)="inc(i)" [disabled]="busy() || i.quantity >= i.availableQty" class="w-7 h-7 text-slate-600 hover:bg-slate-50 disabled:opacity-40">+</button>
                      </div>
                      <span class="text-sm font-semibold text-slate-900">{{ i.lineTotal | currency:'INR':'symbol':'1.0-0' }}</span>
                    </div>
                  </div>
                  <button type="button" (click)="remove(i)" [disabled]="busy()" aria-label="Remove" class="text-slate-300 hover:text-red-500 text-lg self-start leading-none">×</button>
                </div>
              }
            </div>
            <div class="border-t border-slate-200 p-4 space-y-3">
              <div class="flex items-center justify-between text-sm">
                <span class="text-slate-500">Subtotal</span>
                <span class="font-semibold text-slate-900">{{ cart.subtotal() | currency:'INR':'symbol':'1.0-0' }}</span>
              </div>
              <a routerLink="/checkout" (click)="cart.closeDrawer()" class="block text-center bg-primary hover:bg-primary-dark text-white font-medium py-2.5 rounded-lg">Checkout</a>
              <a routerLink="/cart" (click)="cart.closeDrawer()" class="block text-center border border-slate-300 hover:bg-slate-50 text-slate-700 font-medium py-2 rounded-lg text-sm">View cart</a>
            </div>
          }
        </aside>
      </div>
    }
  `,
})
export class CartDrawerComponent {
  readonly cart = inject(CartService);
  private readonly busySig = signal(false);
  readonly busy = this.busySig.asReadonly();

  inc(i: CartItem): void { this.mut(this.cart.updateQty(i.cartItemId, i.quantity + 1)); }
  dec(i: CartItem): void { if (i.quantity <= 1) { this.remove(i); return; } this.mut(this.cart.updateQty(i.cartItemId, i.quantity - 1)); }
  remove(i: CartItem): void { this.mut(this.cart.remove(i.cartItemId)); }

  private mut(obs: Observable<unknown>): void {
    this.busySig.set(true);
    obs.subscribe({ next: () => this.busySig.set(false), error: () => this.busySig.set(false) });
  }
}
