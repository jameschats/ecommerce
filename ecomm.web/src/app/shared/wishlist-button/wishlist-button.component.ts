import { Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { WishlistService } from '../../core/services/wishlist.service';

/** Heart toggle for adding/removing a product from the wishlist. Redirects guests to login. */
@Component({
  selector: 'app-wishlist-button',
  template: `
    <button type="button" (click)="toggle($event)"
      [attr.aria-label]="active() ? 'Remove from wishlist' : 'Add to wishlist'"
      [attr.title]="active() ? 'In your wishlist' : 'Save to wishlist'"
      class="grid place-items-center rounded-full bg-white/90 hover:bg-white shadow-sm border border-slate-200 transition"
      [style.width.px]="size()" [style.height.px]="size()">
      <svg viewBox="0 0 24 24" [attr.width]="size() * 0.55" [attr.height]="size() * 0.55"
        [attr.fill]="active() ? 'currentColor' : 'none'" stroke="currentColor" stroke-width="2"
        [class]="active() ? 'text-red-500' : 'text-slate-500'">
        <path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.6l-1-1a5.5 5.5 0 1 0-7.8 7.8l1 1L12 21l7.8-7.6 1-1a5.5 5.5 0 0 0 0-7.8z"/>
      </svg>
    </button>
  `,
})
export class WishlistButtonComponent {
  private readonly wishlist = inject(WishlistService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly productId = input.required<number>();
  readonly size = input(36);

  readonly active = computed(() => this.wishlist.has(this.productId()));

  toggle(event: Event): void {
    event.preventDefault();
    event.stopPropagation(); // usually sits inside a card link
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { redirect: this.router.url } });
      return;
    }
    const id = this.productId();
    (this.wishlist.has(id) ? this.wishlist.remove(id) : this.wishlist.add(id)).subscribe({ error: () => {} });
  }
}
