import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CatalogService, PublicCollection } from '../../../core/services/catalog.service';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';
import { SITE_URL } from '../../../core/api.config';

@Component({
  selector: 'app-collection',
  imports: [RouterLink, DecimalPipe],
  template: `
    <div class="page-container py-8">
      @if (loading()) { <p class="text-slate-400">Loading…</p> }
      @else if (collection(); as c) {
        <h1 class="text-2xl font-bold text-slate-900">{{ c.name }}</h1>
        @if (c.description) { <p class="text-slate-500 mt-1 mb-6 max-w-2xl">{{ c.description }}</p> } @else { <div class="mb-6"></div> }

        @if (c.products.length) {
          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
            @for (p of c.products; track p.productId) {
              <a [routerLink]="['/product', p.slug]" class="block bg-white border border-slate-200 rounded-xl overflow-hidden hover:shadow-md transition">
                <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                  @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                </div>
                <div class="p-3"><div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div><div class="text-slate-900 font-bold mt-1">₹{{ p.price | number:'1.0-2' }}</div></div>
              </a>
            }
          </div>
        } @else { <p class="text-slate-400">No products in this collection yet.</p> }
      } @else { <p class="text-slate-500">Collection not found.</p> }
    </div>
  `,
})
export class CollectionComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly theme = inject(ThemeService);
  private readonly siteUrl = inject(SITE_URL);

  readonly collection = signal<PublicCollection | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.route.paramMap.subscribe((pm) => {
      const slug = pm.get('slug') ?? '';
      this.loading.set(true);
      this.catalog.getCollection(slug).subscribe((c) => {
        this.collection.set(c);
        this.loading.set(false);
        if (c) this.seo.setMeta({ title: c.metaTitle?.trim() || `${c.name} — ${this.theme.storeName() || 'our store'}`, description: c.metaDescription?.trim() || c.description || c.name, url: `${this.siteUrl}/collection/${c.slug}` });
      });
    });
  }
}
