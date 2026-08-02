import { Component, computed, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MenuItem } from '../../core/services/catalog.service';

interface DrillEntry { label: string; url: string; item: MenuItem; hasChildren: boolean; }
interface DrillSection { heading: string | null; entries: DrillEntry[]; }

/**
 * Mobile drill-down navigation (Phase 4 of the mega-menu plan). Built as its own component, not a
 * shrunk-down version of the desktop hover panel — there was no mobile navigation pattern at all before
 * this (no hamburger, no drawer; the category bar just horizontal-scrolled at every viewport width),
 * so subcategories and mega-menu content were undiscoverable on mobile through the header, period.
 * Hover doesn't exist on touch, so this is a tap-to-drill-in / tap-back pattern instead: a slide-in
 * full-screen panel, one level at a time, mega-menu columns rendered as labeled sections rather than a
 * side-by-side grid (no room for that on a phone width).
 */
@Component({
  selector: 'app-mobile-nav-drawer',
  imports: [RouterLink],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-50 md:hidden">
        <div class="absolute inset-0 bg-black/40" (click)="close()"></div>
        <div class="absolute inset-y-0 left-0 w-[85vw] max-w-sm bg-white flex flex-col">
          <div class="flex items-center gap-2 px-4 h-14 border-b border-slate-100 shrink-0">
            @if (drillPath().length) {
              <button type="button" (click)="back()" aria-label="Back" class="text-slate-500 px-1 -ml-1">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M15 18l-6-6 6-6"/></svg>
              </button>
            }
            <span class="font-semibold text-slate-800 flex-1 truncate">{{ currentHeading() }}</span>
            <button type="button" (click)="close()" aria-label="Close menu" class="text-slate-400 text-2xl leading-none px-1">×</button>
          </div>

          <div class="flex-1 overflow-y-auto py-2">
            @for (section of levelSections(); track section.heading) {
              @if (section.heading) {
                <p class="px-4 pt-3 pb-1 text-xs font-semibold text-slate-400 uppercase">{{ section.heading }}</p>
              }
              @for (entry of section.entries; track entry.label) {
                @if (entry.hasChildren) {
                  <button type="button" (click)="drillInto(entry.item)" class="w-full flex items-center justify-between px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50">
                    {{ entry.label }}
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 6l6 6-6 6"/></svg>
                  </button>
                } @else {
                  <a [routerLink]="entry.url" (click)="close()" class="block px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50">{{ entry.label }}</a>
                }
              }
            }
            @if (!drillPath().length) {
              <a routerLink="/products" (click)="close()" class="block px-4 py-2.5 text-sm font-medium text-primary border-t border-slate-100 mt-2 pt-3">All products</a>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class MobileNavDrawerComponent {
  items = input<MenuItem[]>([]);
  open = input(false);
  closed = output<void>();

  readonly drillPath = signal<MenuItem[]>([]);

  readonly currentHeading = computed(() => {
    const path = this.drillPath();
    return path.length ? path[path.length - 1].label : 'Menu';
  });

  readonly levelSections = computed<DrillSection[]>(() => {
    const path = this.drillPath();
    if (!path.length) return [{ heading: null, entries: this.toEntries(this.items()) }];

    const current = path[path.length - 1];
    if (current.megaMenu?.columns?.length) {
      const sections: DrillSection[] = current.megaMenu.columns.map((col) => ({ heading: col.heading, entries: this.toEntries(col.links) }));
      const promo = current.megaMenu.promo;
      if (promo) {
        sections.push({
          heading: null,
          entries: [{ label: promo.heading, url: promo.link || '/products', item: { label: promo.heading, url: promo.link || '/products' }, hasChildren: false }],
        });
      }
      return sections;
    }
    return [{ heading: null, entries: this.toEntries(current.children ?? []) }];
  });

  private toEntries(items: MenuItem[]): DrillEntry[] {
    return items.map((i) => ({ label: i.label, url: i.url, item: i, hasChildren: !!(i.children?.length || i.megaMenu?.columns?.length) }));
  }

  drillInto(item: MenuItem): void { this.drillPath.set([...this.drillPath(), item]); }
  back(): void { this.drillPath.set(this.drillPath().slice(0, -1)); }
  close(): void { this.closed.emit(); this.drillPath.set([]); }
}
