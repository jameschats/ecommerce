import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { CollectionPageStore } from './collection-page.store';

/** Collection title + product count + (on the all-products view) the category showcase. */
@Component({
  selector: 'app-collection-header',
  imports: [RouterLink],
  template: `
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-slate-900">{{ store.activeCategory()?.name ?? 'All calendars' }}</h1>
      @if (store.result()) {
        <p class="text-sm text-slate-500 mt-1">{{ store.result()!.totalCount }} product(s)</p>
      }
    </div>

    @if (!store.activeCategory() && store.categories().length) {
      <div class="grid grid-cols-3 sm:grid-cols-4 lg:grid-cols-6 gap-4 mb-8">
        @for (c of store.categories(); track c.categoryId) {
          <a [routerLink]="['/category', c.slug]" class="group text-center">
            <div class="aspect-square rounded-2xl overflow-hidden bg-slate-100 border border-slate-200">
              @if (c.imageUrl) { <img [src]="c.imageUrl" [alt]="c.name" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
            </div>
            <p class="mt-2 text-sm font-medium text-slate-700 group-hover:text-primary">{{ c.name }}</p>
          </a>
        }
      </div>
    }
  `,
})
export class CollectionHeaderComponent {
  readonly store = inject(CollectionPageStore);
}

/** Filter bar + category sidebar + product grid + pagination. */
@Component({
  selector: 'app-collection-grid',
  imports: [FormsModule, RouterLink, ProductCardComponent],
  template: `
    <div class="flex flex-wrap items-center gap-3 mb-6">
      <input type="search" [(ngModel)]="store.searchText" (keyup.enter)="store.applyFilters()" placeholder="Search products…"
        class="flex-1 min-w-[200px] rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />

      <select [(ngModel)]="store.brandId" (ngModelChange)="store.applyFilters()"
        class="rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary">
        <option [ngValue]="''">All brands</option>
        @for (b of store.brands(); track b.brandId) { <option [ngValue]="b.brandId">{{ b.name }}</option> }
      </select>

      <select [(ngModel)]="store.sort" (ngModelChange)="store.applyFilters()"
        class="rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary">
        <option value="">Newest</option>
        <option value="price">Price: low to high</option>
        <option value="price_desc">Price: high to low</option>
        <option value="name">Name</option>
      </select>
    </div>

    <div class="flex gap-6">
      <aside class="hidden md:block w-48 shrink-0">
        <h2 class="text-xs font-semibold text-slate-400 uppercase mb-2">Categories</h2>
        <ul class="space-y-1 text-sm">
          <li><a routerLink="/products" class="text-slate-600 hover:text-primary">All</a></li>
          @for (c of store.categories(); track c.categoryId) {
            <li>
              <a [routerLink]="['/category', c.slug]"
                 class="hover:text-primary"
                 [class]="store.activeCategory()?.categoryId === c.categoryId ? 'text-primary font-medium' : 'text-slate-600'">
                {{ c.name }}
              </a>
            </li>
          }
        </ul>
      </aside>

      <div class="flex-1">
        @if (store.loading()) {
          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            @for (s of store.skeletons; track s) {
              <div class="bg-white border border-slate-200 rounded-xl overflow-hidden animate-pulse">
                <div class="aspect-square bg-slate-100"></div>
                <div class="p-3 space-y-2">
                  <div class="h-2 w-1/3 bg-slate-100 rounded"></div>
                  <div class="h-3 w-3/4 bg-slate-100 rounded"></div>
                  <div class="h-4 w-1/2 bg-slate-100 rounded"></div>
                </div>
              </div>
            }
          </div>
        } @else if (store.result() && store.result()!.items.length === 0) {
          <div class="py-20 text-center text-slate-400">No products found.</div>
        } @else {
          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            @for (p of store.result()!.items; track p.productId) {
              <app-product-card [product]="p" />
            }
          </div>

          @if (store.result()!.totalPages > 1) {
            <div class="flex justify-center gap-1 mt-8">
              @for (pg of store.pages; track pg) {
                <button type="button" (click)="store.goToPage(pg)"
                  class="w-9 h-9 rounded-lg text-sm border"
                  [class]="pg === store.result()!.page ? 'bg-primary text-white border-primary' : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-50'">
                  {{ pg }}
                </button>
              }
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class CollectionGridComponent {
  readonly store = inject(CollectionPageStore);
}
