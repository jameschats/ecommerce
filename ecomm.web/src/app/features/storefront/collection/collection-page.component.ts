import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { distinctUntilChanged, map, switchMap } from 'rxjs/operators';
import { ThemeSection, ThemeService } from '../../../core/services/theme.service';
import { StorefrontSectionComponent } from '../storefront-section.component';
import { SectionSlot, slotsFrom } from '../section-slot';
import { CollectionPageStore } from './collection-page.store';
import {
  CollectionBreadcrumbsComponent,
  CollectionCategoriesComponent,
  CollectionGridComponent,
  CollectionHeaderComponent,
} from './collection-sections.component';
import { SearchBarComponent } from '../search/search-sections.component';

/** Default `collection` layout when the theme defines no collection template. Matches today's page. */
// CollectionCategories (a "Shop by category" tile grid) used to be a default here too, but it's now
// redundant with the faceted-search sidebar's own Category filter on this same page — showing the
// same categories two ways (a tile wall AND a filter) on the "all products" listing isn't standard;
// tile grids belong on the homepage. Still available as an opt-in section for a theme that wants it.
const DEFAULT_COLLECTION_SECTIONS = ['CollectionHeader', 'CollectionGrid'];

interface EmptyStateCfg { heading?: string; body?: string; buttonText?: string; buttonLink?: string; }

/**
 * Section-composed collection/listing page (S3). Serves /products, /category/:slug
 * and search (?search=). Renders the published theme's `collection` template as an
 * ordered section list, falling back to the built-in order when none is authored.
 *
 * Search results (`?search=`) prefer an authored `search` template — a distinct template key that
 * existed in the section-type schema but was previously never actually requested (every visit,
 * search or not, always loaded `collection`, so a theme author's `search` template — and any
 * `EmptyState` "no results" section on it — could never render). Falls back to `collection` when
 * no `search` template is authored, same "fall back when unauthored" convention used everywhere
 * else in this theme system.
 */
@Component({
  selector: 'app-collection-page',
  imports: [
    RouterLink, StorefrontSectionComponent, CollectionBreadcrumbsComponent, CollectionHeaderComponent,
    CollectionCategoriesComponent, CollectionGridComponent, SearchBarComponent,
  ],
  providers: [CollectionPageStore],
  template: `
    <section class="page-container py-8">
      @if (isSearch() && !store.loading() && store.result()?.totalCount === 0 && hasEmptyState()) {
        <div class="text-center py-20 bg-white rounded-xl border border-slate-200">
          <div class="mx-auto w-16 h-16 rounded-full bg-slate-100 flex items-center justify-center text-slate-400 mb-4">
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="11" cy="11" r="7"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>
          </div>
          <p class="text-slate-600 font-medium">{{ empty().heading || 'No results found' }}</p>
          @if (empty().body) { <p class="text-sm text-slate-400 mt-1">{{ empty().body }}</p> }
          @if (empty().buttonText) {
            <a [routerLink]="empty().buttonLink || '/products'" class="inline-block mt-5 btn-primary px-5 py-2.5">{{ empty().buttonText }}</a>
          }
        </div>
      } @else {
        @for (slot of slots(); track $index) {
          @switch (slot.type) {
            @case ('Breadcrumbs') { <app-collection-breadcrumbs /> }
            @case ('CollectionHeader') { <app-collection-header [settingsJson]="slot.data?.settings ?? null" /> }
            @case ('CollectionCategories') { <app-collection-categories [settingsJson]="slot.data?.settings ?? null" /> }
            @case ('CollectionGrid') { <app-collection-grid [settingsJson]="slot.data?.settings ?? null" /> }
            @case ('SearchBar') { <app-search-bar [settingsJson]="slot.data?.settings ?? null" /> }
            @case ('SearchResults') { <app-collection-grid [settingsJson]="slot.data?.settings ?? null" /> }
            @default { @if (slot.data) { <app-storefront-section [section]="slot.data" /> } }
          }
        }
      }
    </section>
  `,
})
export class CollectionPageComponent implements OnInit {
  readonly store = inject(CollectionPageStore);
  private readonly theme = inject(ThemeService);
  private readonly route = inject(ActivatedRoute);

  readonly isSearch = signal(false);
  readonly slots = signal<SectionSlot[]>(slotsFrom([], DEFAULT_COLLECTION_SECTIONS));
  private readonly rawSections = signal<ThemeSection[]>([]);

  /** Authored EmptyState settings ("no results") — only meaningful (and only ever shown) on a
   *  search with zero results; same opt-in pattern as the cart/404 EmptyState sections. */
  readonly hasEmptyState = computed(() => this.rawSections().some((s) => s.sectionType === 'EmptyState'));
  readonly empty = computed<EmptyStateCfg>(() => {
    const es = this.rawSections().find((s) => s.sectionType === 'EmptyState');
    try { return es?.settings ? JSON.parse(es.settings) : {}; } catch { return {}; }
  });

  ngOnInit(): void {
    this.route.queryParamMap.pipe(
      map((q) => !!q.get('search')),
      distinctUntilChanged(),
      switchMap((isSearch) => {
        this.isSearch.set(isSearch);
        if (!isSearch) return this.theme.getTemplate('collection');
        return this.theme.getTemplateInfo('search').pipe(
          switchMap((info) => (info.authored ? of(info.sections) : this.theme.getTemplate('collection'))),
        );
      }),
    ).subscribe((sections) => {
      this.rawSections.set(sections);
      this.slots.set(slotsFrom(sections.filter((s) => s.sectionType !== 'EmptyState'), DEFAULT_COLLECTION_SECTIONS));
      // Read the grid's own "products per page" setting once, before starting the query pipeline,
      // so the very first fetch already uses it instead of racing a settings update. SearchResults
      // reuses CollectionGrid's settings model verbatim, so both section types are checked here.
      const gridSection = sections.find((s) => s.sectionType === 'CollectionGrid' || s.sectionType === 'SearchResults');
      let pageSize = 12;
      if (gridSection?.settings) {
        try { pageSize = Number(JSON.parse(gridSection.settings).productsPerPage) || 12; } catch { /* keep default */ }
      }
      this.store.init(pageSize);
    });
  }
}
