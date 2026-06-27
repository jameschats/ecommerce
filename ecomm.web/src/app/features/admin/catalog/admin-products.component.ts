import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PagedResult } from '../../../core/models/api-response.model';
import { ProductListItem } from '../../../core/models/catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';

@Component({
  selector: 'app-admin-products',
  imports: [FormsModule, RouterLink, CurrencyPipe],
  template: `
    <div class="max-w-6xl mx-auto p-6">
      <div class="flex items-center justify-between mb-4">
        <h1 class="text-xl font-bold text-slate-900">Products</h1>
        <a routerLink="/admin/products/new" class="btn-primary">+ New product</a>
      </div>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="mb-4">
        <input [(ngModel)]="search" (keyup.enter)="applySearch()" placeholder="Search by name or SKU…"
          class="input max-w-sm" />
      </div>

      <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
        @if (loading()) { <div class="p-10 text-center text-slate-400">Loading…</div> }
        @else {
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-slate-500 text-left">
              <tr>
                <th class="px-4 py-2">Product</th><th class="px-4 py-2">SKU</th>
                <th class="px-4 py-2">Category</th><th class="px-4 py-2">Price</th>
                <th class="px-4 py-2">Status</th><th class="px-4 py-2"></th>
              </tr>
            </thead>
            <tbody>
              @for (p of result()?.items ?? []; track p.productId) {
                <tr class="border-t border-slate-100">
                  <td class="px-4 py-2">
                    <div class="flex items-center gap-2">
                      @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-9 h-9 rounded object-cover bg-slate-100" /> }
                      <span class="text-slate-800">{{ p.name }}</span>
                      @if (p.isFeatured) { <span class="text-[10px] bg-amber-100 text-amber-700 rounded px-1.5 py-0.5">Featured</span> }
                    </div>
                  </td>
                  <td class="px-4 py-2 text-slate-400">{{ p.sku }}</td>
                  <td class="px-4 py-2 text-slate-600">{{ p.categoryName }}</td>
                  <td class="px-4 py-2 text-slate-800">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</td>
                  <td class="px-4 py-2">
                    <span class="text-xs rounded px-2 py-0.5"
                      [class]="p.status === 'Active' ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'">{{ p.status }}</span>
                  </td>
                  <td class="px-4 py-2 text-right whitespace-nowrap">
                    <a [routerLink]="['/admin/products', p.productId]" class="text-blue-600 hover:underline mr-3">Edit</a>
                    <button type="button" (click)="remove(p)" class="text-red-600 hover:underline">Delete</button>
                  </td>
                </tr>
              }
              @if ((result()?.items?.length ?? 0) === 0) { <tr><td colspan="6" class="px-4 py-10 text-center text-slate-400">No products found.</td></tr> }
            </tbody>
          </table>
        }
      </div>

      @if ((result()?.totalPages ?? 0) > 1) {
        <div class="flex justify-center gap-1 mt-6">
          @for (pg of pages; track pg) {
            <button type="button" (click)="goTo(pg)" class="w-9 h-9 rounded-lg text-sm border"
              [class]="pg === result()!.page ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-slate-600 border-slate-300'">{{ pg }}</button>
          }
        </div>
      }
    </div>
  `,
})
export class AdminProductsComponent implements OnInit {
  private readonly api = inject(AdminCatalogService);

  readonly result = signal<PagedResult<ProductListItem> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  search = '';
  private page = 1;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listProducts({ search: this.search || undefined, page: this.page, pageSize: 20 }).subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  applySearch(): void { this.page = 1; this.load(); }
  goTo(p: number): void { this.page = p; this.load(); }

  get pages(): number[] {
    const r = this.result();
    return r ? Array.from({ length: r.totalPages }, (_, i) => i + 1) : [];
  }

  remove(p: ProductListItem): void {
    if (!confirm(`Delete product "${p.name}"?`)) return;
    this.api.deleteProduct(p.productId).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e?.error?.message ?? 'Delete failed.'),
    });
  }
}
