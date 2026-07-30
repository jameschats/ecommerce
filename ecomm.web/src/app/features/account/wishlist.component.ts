import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ProductListItem } from '../../core/models/catalog.model';
import { CartService } from '../../core/services/cart.service';
import { WishlistService } from '../../core/services/wishlist.service';
import { ProductCardComponent } from '../../shared/product-card/product-card.component';

@Component({
  selector: 'app-wishlist',
  imports: [RouterLink, ProductCardComponent],
  template: `
    <h2 class="font-semibold text-slate-800 mb-4">Wishlist</h2>
    @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

    @if (loading()) {
      <p class="text-slate-400 text-sm">Loading…</p>
    } @else if (!visible().length) {
      <div class="bg-white rounded-xl border border-slate-200 p-10 text-center">
        <p class="text-slate-500">Your wishlist is empty.</p>
        <a routerLink="/order" class="text-primary hover:underline text-sm">Browse products</a>
      </div>
    } @else {
      <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
        @for (p of visible(); track p.productId) {
          <div>
            <app-product-card [product]="p" />
            <button type="button" (click)="moveToCart(p)" [disabled]="!p.inStock || busy() === p.productId"
              class="mt-2 w-full text-sm bg-primary hover:bg-primary-dark disabled:opacity-50 text-white py-2 rounded-lg transition">
              {{ p.inStock ? (busy() === p.productId ? 'Adding…' : 'Move to cart') : 'Out of stock' }}
            </button>
          </div>
        }
      </div>
    }
  `,
})
export class WishlistComponent implements OnInit {
  private readonly wishlist = inject(WishlistService);
  private readonly cart = inject(CartService);
  private readonly router = inject(Router);

  private readonly items = signal<ProductListItem[]>([]);
  readonly loading = signal(true);
  readonly busy = signal<number | null>(null);
  readonly message = signal<string | null>(null);

  // Reactively drop items as they're removed (heart toggle or move-to-cart).
  readonly visible = computed(() => this.items().filter((p) => this.wishlist.has(p.productId)));

  ngOnInit(): void {
    this.wishlist.list().subscribe({
      next: (list) => { this.items.set(list); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  moveToCart(p: ProductListItem): void {
    this.busy.set(p.productId);
    this.message.set(null);
    this.cart.add(p.productId, null, 1).subscribe({
      next: () => {
        this.wishlist.remove(p.productId).subscribe({ error: () => {} });
        this.busy.set(null);
        this.message.set(`Moved "${p.name}" to your cart.`);
      },
      error: () => {
        // Product needs options chosen — send them to the product page.
        this.busy.set(null);
        this.router.navigate(['/product', p.slug]);
      },
    });
  }
}
