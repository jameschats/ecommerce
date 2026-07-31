import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CompareService } from '../../core/services/compare.service';

/** Global floating tray showing how many products are queued to compare, mounted once at the
 *  app root (same pattern as app-quick-view) so it stays visible across every storefront page.
 *  Sits above bottom-4 to clear the PDP's sticky add-to-cart bar when both are on screen. */
@Component({
  selector: 'app-compare-bar',
  imports: [RouterLink],
  template: `
    @if (compare.ids().length > 0) {
      <div class="fixed bottom-20 right-4 z-40 bg-slate-900 text-white rounded-xl shadow-lg px-4 py-2.5 flex items-center gap-3">
        <span class="text-sm font-medium">{{ compare.ids().length }}/4 to compare</span>
        <a routerLink="/compare" class="bg-white text-slate-900 text-sm font-semibold px-3 py-1.5 rounded-lg hover:bg-slate-100">Compare</a>
        <button type="button" (click)="compare.clear()" aria-label="Clear compare list" class="text-white/60 hover:text-white text-lg leading-none">×</button>
      </div>
    }
  `,
})
export class CompareBarComponent {
  readonly compare = inject(CompareService);
}
