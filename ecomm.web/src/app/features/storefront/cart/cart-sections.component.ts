import { CurrencyPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartPageStore } from './cart-page.store';

/** The cart line items with quantity steppers + remove. */
@Component({
  selector: 'app-cart-items',
  imports: [RouterLink, CurrencyPipe],
  template: `
    <div class="bg-white rounded-xl border border-slate-200 divide-y divide-slate-100">
      @for (it of store.items(); track it.cartItemId) {
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
                <button type="button" (click)="store.dec(it)" [disabled]="store.busy()" class="w-8 h-8 text-slate-600 hover:bg-slate-50 disabled:opacity-40">−</button>
                <span class="w-9 text-center text-sm">{{ it.quantity }}</span>
                <button type="button" (click)="store.inc(it)" [disabled]="store.busy() || it.quantity >= it.availableQty" class="w-8 h-8 text-slate-600 hover:bg-slate-50 disabled:opacity-40">+</button>
              </div>
              <button type="button" (click)="store.remove(it)" [disabled]="store.busy()" class="text-sm text-slate-500 hover:text-red-600">Remove</button>
            </div>
          </div>
          <div class="text-right font-semibold text-slate-800 whitespace-nowrap">{{ it.lineTotal | currency:'INR':'symbol':'1.0-0' }}</div>
        </div>
      }
    </div>
  `,
})
export class CartItemsComponent {
  readonly store = inject(CartPageStore);
}

/** Order summary: subtotal, total, checkout button. */
@Component({
  selector: 'app-cart-summary',
  imports: [RouterLink, CurrencyPipe],
  template: `
    <div class="bg-white rounded-xl border border-slate-200 p-5 lg:sticky lg:top-20">
      <h2 class="font-semibold text-slate-800 mb-3">Order summary</h2>
      <div class="flex justify-between text-sm text-slate-600 mb-1"><span>Subtotal ({{ store.count() }} items)</span><span>{{ store.subtotal() | currency:'INR':'symbol':'1.0-0' }}</span></div>
      <div class="flex justify-between text-sm text-slate-500 mb-3"><span>Shipping</span><span>Calculated at checkout</span></div>
      <div class="border-t border-slate-100 pt-3 flex justify-between font-bold text-slate-900"><span>Total</span><span>{{ store.subtotal() | currency:'INR':'symbol':'1.0-0' }}</span></div>
      @if (store.taxMode() === 'Inclusive') { <p class="text-xs text-slate-400 mt-1">Inclusive of all taxes</p> }
      <button type="button" (click)="store.checkout()" [disabled]="!store.canCheckout()" class="btn-primary w-full mt-4 py-3 disabled:opacity-50 disabled:cursor-not-allowed">Proceed to checkout</button>
      @if (store.checkoutNote()) { <p class="text-xs text-amber-600 mt-2 text-center">{{ store.checkoutNote() }}</p> }
      <a routerLink="/products" class="block text-center text-sm text-primary hover:underline mt-3">Continue shopping</a>
      @if (store.error(); as e) { <p class="text-xs text-red-600 mt-2 text-center">{{ e }}</p> }
    </div>
  `,
})
export class CartSummaryComponent {
  readonly store = inject(CartPageStore);
}
