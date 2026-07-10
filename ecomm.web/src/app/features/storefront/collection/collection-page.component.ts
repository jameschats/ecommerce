import { Component, OnInit, inject, signal } from '@angular/core';
import { ThemeService } from '../../../core/services/theme.service';
import { StorefrontSectionComponent } from '../storefront-section.component';
import { SectionSlot, slotsFrom } from '../section-slot';
import { CollectionPageStore } from './collection-page.store';
import { CollectionGridComponent, CollectionHeaderComponent } from './collection-sections.component';

/** Default `collection` layout when the theme defines no collection template. Matches today's page. */
const DEFAULT_COLLECTION_SECTIONS = ['CollectionHeader', 'CollectionGrid'];

/**
 * Section-composed collection/listing page (S3). Serves /products, /category/:slug
 * and search (?search=). Renders the published theme's `collection` template as an
 * ordered section list, falling back to the built-in order when none is authored.
 */
@Component({
  selector: 'app-collection-page',
  imports: [StorefrontSectionComponent, CollectionHeaderComponent, CollectionGridComponent],
  providers: [CollectionPageStore],
  template: `
    <section class="page-container py-8">
      @for (slot of slots(); track $index) {
        @switch (slot.type) {
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

  readonly slots = signal<SectionSlot[]>(slotsFrom([], DEFAULT_COLLECTION_SECTIONS));

  ngOnInit(): void {
    this.theme.getTemplate('collection').subscribe((sections) => this.slots.set(slotsFrom(sections, DEFAULT_COLLECTION_SECTIONS)));
    this.store.init();
  }
}
