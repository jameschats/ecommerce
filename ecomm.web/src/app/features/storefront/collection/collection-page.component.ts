import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { distinctUntilChanged, map, switchMap } from 'rxjs/operators';
import { ThemeService } from '../../../core/services/theme.service';
import { StorefrontSectionComponent } from '../storefront-section.component';
import { SectionSlot, slotsFrom } from '../section-slot';
import { CollectionPageStore } from './collection-page.store';
import { CollectionBreadcrumbsComponent, CollectionGridComponent, CollectionHeaderComponent } from './collection-sections.component';

/** Default `collection` layout when the theme defines no collection template. Matches today's page. */
const DEFAULT_COLLECTION_SECTIONS = ['CollectionHeader', 'CollectionGrid'];

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
  imports: [StorefrontSectionComponent, CollectionBreadcrumbsComponent, CollectionHeaderComponent, CollectionGridComponent],
  providers: [CollectionPageStore],
  template: `
    <section class="page-container py-8">
      @for (slot of slots(); track $index) {
        @switch (slot.type) {
          @case ('Breadcrumbs') { <app-collection-breadcrumbs /> }
          @case ('CollectionHeader') { <app-collection-header /> }
          @case ('CollectionGrid') { <app-collection-grid /> }
          @default { @if (slot.data) { <app-storefront-section [section]="slot.data" /> } }
        }
      }
    </section>
  `,
})
export class CollectionPageComponent implements OnInit {
  readonly store = inject(CollectionPageStore);
  private readonly theme = inject(ThemeService);
  private readonly route = inject(ActivatedRoute);

  readonly slots = signal<SectionSlot[]>(slotsFrom([], DEFAULT_COLLECTION_SECTIONS));

  ngOnInit(): void {
    this.route.queryParamMap.pipe(
      map((q) => !!q.get('search')),
      distinctUntilChanged(),
      switchMap((isSearch) => {
        if (!isSearch) return this.theme.getTemplate('collection');
        return this.theme.getTemplateInfo('search').pipe(
          switchMap((info) => (info.authored ? of(info.sections) : this.theme.getTemplate('collection'))),
        );
      }),
    ).subscribe((sections) => this.slots.set(slotsFrom(sections, DEFAULT_COLLECTION_SECTIONS)));
    this.store.init();
  }
}
