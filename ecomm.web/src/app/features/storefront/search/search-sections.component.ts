import { Component, computed, inject, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CollectionPageStore } from '../collection/collection-page.store';

/** Same parse-with-fallback pattern every other dynamic section file uses. */
function parseSettings(json: string | null | undefined): any {
  if (!json) return {};
  try { return JSON.parse(json) ?? {}; } catch { return {}; }
}

/** The storefront search box, `search` template. Shares CollectionPageStore with SearchResults
 *  (aliased to CollectionGridComponent) so typing a new query and submitting re-runs the exact
 *  same product+facets pipeline the results grid already renders from — no separate search state. */
@Component({
  selector: 'app-search-bar',
  imports: [FormsModule],
  template: `
    <div class="mb-6">
      <form (ngSubmit)="submit()" class="flex gap-2 max-w-xl">
        <input type="search" name="q" [(ngModel)]="store.searchText" [placeholder]="settings().placeholder || 'Search products…'"
          class="flex-1 rounded-lg border border-slate-300 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
        <button type="submit" class="btn-primary px-5">Search</button>
      </form>
      @if (store.searchText && !store.loading() && store.result()) {
        <p class="mt-3 text-sm text-slate-500">
          {{ store.result()!.totalCount }} result{{ store.result()!.totalCount === 1 ? '' : 's' }} for "{{ store.searchText }}"
        </p>
      }
    </div>
  `,
})
export class SearchBarComponent {
  readonly store = inject(CollectionPageStore);
  settingsJson = input<string | null>(null);
  readonly settings = computed(() => parseSettings(this.settingsJson()));

  submit(): void { this.store.applyFilters(); }
}
