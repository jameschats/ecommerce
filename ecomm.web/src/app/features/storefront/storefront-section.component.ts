import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CatalogService } from '../../core/services/catalog.service';
import { BuilderSection } from '../../core/services/cms.service';
import { Category, ProductListItem } from '../../core/models/catalog.model';

/**
 * Renders one storefront section from its type + settings/blocks JSON.
 * The single source of truth for how a section looks (matches the Section-Type
 * schema on the backend). RichText is bound via [innerHTML]; it was sanitized
 * server-side, and Angular sanitizes again here.
 */
@Component({
  selector: 'app-storefront-section',
  imports: [RouterLink],
  template: `
    @switch (section().sectionType) {
      @case ('Hero') {
        <section class="relative">
          @for (b of blocks(); track $index) {
            <div class="relative min-h-[320px] flex items-center justify-center text-center bg-slate-900 text-white"
                 [style.background-image]="b.image ? 'url(' + b.image + ')' : null" style="background-size:cover;background-position:center">
              <div class="bg-black/30 absolute inset-0"></div>
              <div class="relative p-8 max-w-2xl">
                @if (b.heading) { <h2 class="text-3xl sm:text-4xl font-bold">{{ b.heading }}</h2> }
                @if (b.subheading) { <p class="mt-2 text-slate-200">{{ b.subheading }}</p> }
                @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-4 px-5 py-2 rounded-lg bg-primary text-white font-medium">{{ b.buttonText }}</a> }
              </div>
            </div>
          }
          @if (!blocks().length) { <div class="min-h-[200px] grid place-items-center text-slate-300 bg-slate-100">Add slides to this hero</div> }
        </section>
      }
      @case ('RichText') {
        <div class="max-w-3xl mx-auto px-4 py-8 prose" [style.text-align]="s().align || 'left'" [innerHTML]="s().content"></div>
      }
      @case ('ImageWithText') {
        <section class="max-w-5xl mx-auto px-4 py-10 grid sm:grid-cols-2 gap-8 items-center" [class.sm:flex-row-reverse]="s().imageSide === 'right'">
          @if (s().image) { <img [src]="s().image" alt="" class="rounded-xl w-full object-cover" [class.sm:order-2]="s().imageSide === 'right'" /> }
          <div>
            @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900">{{ s().heading }}</h2> }
            @if (s().body) { <p class="mt-2 text-slate-600">{{ s().body }}</p> }
            @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="inline-block mt-4 px-5 py-2 rounded-lg bg-primary text-white font-medium">{{ s().buttonText }}</a> }
          </div>
        </section>
      }
      @case ('Testimonials') {
        <section class="max-w-5xl mx-auto px-4 py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 text-center mb-6">{{ s().heading }}</h2> }
          <div class="grid sm:grid-cols-3 gap-4">
            @for (b of blocks(); track $index) {
              <div class="bg-white border border-slate-200 rounded-xl p-5">
                <div class="text-amber-400">{{ stars(b.rating) }}</div>
                <p class="text-slate-600 mt-2">"{{ b.quote }}"</p>
                <div class="text-sm font-medium text-slate-800 mt-3">— {{ b.author }}</div>
              </div>
            }
          </div>
        </section>
      }
      @case ('CtaNewsletter') {
        <section class="py-12 text-center text-white" [style.background-color]="s().backgroundColor || '#111827'">
          @if (s().heading) { <h2 class="text-2xl font-bold">{{ s().heading }}</h2> }
          @if (s().subtext) { <p class="mt-1 text-white/80">{{ s().subtext }}</p> }
          @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="inline-block mt-4 px-6 py-2 rounded-lg bg-white text-slate-900 font-medium">{{ s().buttonText }}</a> }
        </section>
      }
      @case ('Categories') {
        <section class="max-w-6xl mx-auto px-4 py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5">{{ s().heading }}</h2> }
          <div class="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3">
            @for (c of categories(); track c.categoryId) {
              <a [routerLink]="['/category', c.slug]" class="block border border-slate-200 rounded-xl p-4 text-center hover:border-primary hover:shadow-sm transition">
                @if (c.imageUrl) { <img [src]="c.imageUrl" alt="" class="w-12 h-12 mx-auto object-contain mb-2" /> }
                <div class="text-sm font-medium text-slate-700">{{ c.name }}</div>
              </a>
            }
          </div>
        </section>
      }
      @default {
        <!-- FeaturedProducts / ProductGrid / any product rail -->
        <section class="max-w-6xl mx-auto px-4 py-10">
          @if (s().heading || section().title) { <h2 class="text-2xl font-bold text-slate-900 mb-5">{{ s().heading || section().title }}</h2> }
          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
            @for (p of products(); track p.productId) {
              <a [routerLink]="['/product', p.slug]" class="block bg-white border border-slate-200 rounded-xl overflow-hidden hover:shadow-md transition">
                <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                  @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                </div>
                <div class="p-3">
                  <div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div>
                  <div class="text-slate-900 font-bold mt-1">₹{{ p.price }}</div>
                </div>
              </a>
            }
          </div>
        </section>
      }
    }
  `,
})
export class StorefrontSectionComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  readonly section = input.required<BuilderSection>();

  readonly products = signal<ProductListItem[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly s = computed<any>(() => this.parse<any>(this.section().settings, {}));
  readonly blocks = computed<any[]>(() => this.parse<any[]>(this.section().blocks, []));

  ngOnInit(): void {
    const type = this.section().sectionType;
    if (type === 'Categories') {
      this.catalog.getCategories().subscribe((c) => this.categories.set(c.slice(0, 12)));
    } else if (type === 'FeaturedProducts' || type === 'ProductGrid') {
      const cfg: Record<string, any> = this.s();
      const count = Number(cfg['count']) || 8;
      this.catalog.getProducts({
        pageSize: count,
        isFeatured: cfg['source'] === 'featured' ? true : undefined,
        sort: cfg['source'] === 'newest' ? 'newest' : cfg['source'] === 'bestsellers' ? 'bestsellers' : undefined,
        categoryId: cfg['source'] === 'category' && cfg['categoryId'] ? Number(cfg['categoryId']) : undefined,
      }).subscribe((r) => this.products.set(r.items));
    }
  }

  stars(n: number): string { const r = Math.max(0, Math.min(5, Math.round(n || 0))); return '★★★★★'.slice(0, r) + '☆☆☆☆☆'.slice(0, 5 - r); }

  private parse<T>(json: string | null, fallback: T): T {
    try { return json ? JSON.parse(json) : fallback; } catch { return fallback; }
  }
}
