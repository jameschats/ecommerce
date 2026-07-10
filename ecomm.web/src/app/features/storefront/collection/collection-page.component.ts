import { Component, OnInit, inject, signal } from '@angular/core';
import { ThemeService } from '../../../core/services/theme.service';
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
  imports: [CollectionHeaderComponent, CollectionGridComponent],
  providers: [CollectionPageStore],
  template: `
    <section class="page-container py-8">
      @for (type of sectionTypes(); track $index) {
        @switch (type) {
          @case ('CollectionHeader') { <app-collection-header /> }
          @case ('CollectionGrid') { <app-collection-grid /> }
        }
      }
    </section>
  `,
})
export class CollectionPageComponent implements OnInit {
  readonly store = inject(CollectionPageStore);
  private readonly theme = inject(ThemeService);

  readonly sectionTypes = signal<string[]>(DEFAULT_COLLECTION_SECTIONS);

  ngOnInit(): void {
    this.theme.getTemplate('collection').subscribe((sections) => {
      if (sections.length) this.sectionTypes.set(sections.map((s) => s.sectionType));
    });
    this.store.init();
  }
}
