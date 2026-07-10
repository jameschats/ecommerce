import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { Cart, CartItem } from '../../../core/models/cart.model';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';

/**
 * All state + behaviour for the cart page. Provided at the CartPageComponent level
 * so the (thin) cart section components can inject it. This is the cart-component
 * logic re-homed unchanged — it operates entirely through CartService, so cart
 * mutations and totals behave exactly as before.
 */
@Injectable()
export class CartPageStore {
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
