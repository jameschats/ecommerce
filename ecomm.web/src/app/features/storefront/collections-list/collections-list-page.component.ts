import { Component, OnInit, inject, signal, computed, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { PublicCollectionSummary } from '../../../core/services/catalog.service';
import { CatalogService } from '../../../core/services/catalog.service';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';
import { StorefrontSectionComponent } from '../storefront-section.component';
import { SectionSlot, slotsFrom } from '../section-slot';

/** Default `list-collections` layout when the theme defines no template for it — same "fall back when
 * unauthored" convention used everywhere else in this theme system. */
const DEFAULT_LIST_COLLECTIONS_SECTIONS = ['CollectionsList'];

function parseSettings(json: string | null | undefined): any {
  if (!json) return {};
  try { return JSON.parse(json) ?? {}; } catch { return {}; }
}

/** A tile grid of every active collection — Shopify's "All Collections" index. Previously this
 * template had no public route, no page component, and no renderer at all: it was fully scaffolded in
 * the theme editor (selectable, its own section type + settings) but the editor's own preview-path
 * resolver hardcoded a fallback to /products because there was nowhere real to point it. This is that
 * real page. */
@Component({
  selector: 'app-collections-list-grid',
  imports: [RouterLink],
  template: `
    @if (settings().heading) { <h1 class="text-2xl font-bold text-slate-900 mb-6">{{ settings().heading }}</h1> }
    @if (loading()) {
      <div class="grid grid-cols-2 sm:grid-cols-3 gap-4" [style.--cols]="columns()">
        @for (s of skeletons; track s) {
          <div class="aspect-[4/3] bg-slate-100 rounded-2xl animate-pulse"></div>
        }
      </div>
    } @else if (!collections().length) {
      <div class="py-20 text-center text-slate-400">No collections yet.</div>
    } @else {
      <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-[repeat(var(--cols),minmax(0,1fr))] gap-4" [style.--cols]="columns()">
        @for (c of collections(); track c.collectionId) {
          <a [routerLink]="['/collection', c.slug]" class="group block overflow-hidden sf-card">
            <div class="aspect-[4/3] bg-slate-100 overflow-hidden">
              @if (c.imageUrl) { <img [src]="c.imageUrl" [alt]="c.name" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
            </div>
            <div class="p-3">
              <div class="font-semibold text-slate-800">{{ c.name }}</div>
              <div class="text-xs text-slate-400">{{ c.productCount }} product(s)</div>
            </div>
          </a>
        }
      </div>
    }
  `,
})
export class CollectionsListGridComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  settingsJson = input<string | null>(null);
  readonly settings = computed(() => parseSettings(this.settingsJson()));
  readonly columns = computed(() => Math.max(2, Number(this.settings().columns) || 3));

  readonly collections = signal<PublicCollectionSummary[]>([]);
  readonly loading = signal(true);
  readonly skeletons = Array.from({ length: 6 }, (_, i) => i);

  ngOnInit(): void {
    this.catalog.getCollections().subscribe((c) => { this.collections.set(c); this.loading.set(false); });
  }
}

@Component({
  selector: 'app-collections-list-page',
  imports: [StorefrontSectionComponent, CollectionsListGridComponent],
  template: `
    <section class="page-container py-8">
      @for (slot of slots(); track $index) {
        @switch (slot.type) {
          @case ('CollectionsList') { <app-collections-list-grid [settingsJson]="slot.data?.settings ?? null" /> }
          @default { @if (slot.data) { <app-storefront-section [section]="slot.data" /> } }
        }
      }
    </section>
  `,
})
export class CollectionsListPageComponent implements OnInit {
  private readonly theme = inject(ThemeService);
  private readonly seo = inject(SeoService);
  private readonly router = inject(Router);
  private readonly siteUrl = inject(SITE_URL);

  readonly slots = signal<SectionSlot[]>(slotsFrom([], DEFAULT_LIST_COLLECTIONS_SECTIONS));

  ngOnInit(): void {
    this.theme.getTemplate('list-collections').subscribe((sections) =>
      this.slots.set(slotsFrom(sections, DEFAULT_LIST_COLLECTIONS_SECTIONS)));
    const brand = this.theme.storeName() || 'our store';
    this.seo.setMeta({
      title: `Collections — ${brand}`,
      description: `Browse all collections at ${brand}.`,
      url: this.siteUrl + this.router.url,
    });
  }
}
