import { Component, OnInit, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import {
  Menu, MenuItem, MegaMenu, MegaMenuColumn, NavigationAdminService, UrlRedirect,
} from '../../../core/services/navigation-admin.service';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';
import { CollectionAdminService, AdminCollection } from '../../../core/services/collection-admin.service';
import { CmsService, BuilderPage } from '../../../core/services/cms.service';
import { Category } from '../../../core/models/catalog.model';
import { MediaPickerComponent } from '../../../shared/media-picker/media-picker.component';

type Mode = 'none' | 'simple' | 'mega';

/**
 * Rebuilt for the mega-menu navigation plan (Phase 2). The previous version was two flat <input>s per
 * row and silently stripped children/megaMenu on every save even though the backend already supported
 * nesting — a merchant genuinely could not author a dropdown or mega-menu, only a flat top-level list.
 * This version supports: reorderable top-level items, a "Pick from…" target picker (Category/
 * Collection/Page/Custom URL) that auto-fills label+url, a per-item mode (no dropdown / simple children
 * list / rich mega-menu with named columns + an optional promo tile).
 */
@Component({
  selector: 'app-admin-navigation',
  imports: [FormsModule, DragDropModule, MediaPickerComponent, NgTemplateOutlet],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Navigation</h1>
      <p class="text-sm text-slate-500 mb-4">Edit your storefront menus and set up URL redirects.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <!-- Menus -->
      <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
        <div class="flex gap-2 mb-4">
          @for (m of menus(); track m.handle) {
            <button type="button" (click)="pick(m)" class="text-sm px-3 py-1.5 rounded-lg border"
              [class]="active()?.handle === m.handle ? 'border-primary bg-primary/5 text-primary font-medium' : 'border-slate-200 text-slate-600 hover:bg-slate-50'">{{ m.title }}</button>
          }
        </div>

        @if (active(); as menu) {
          <p class="text-xs text-slate-400 mb-3">
            @if (menu.handle === 'main-menu') { This drives the storefront header's top-level nav — leave it empty to keep today's automatic "every category" bar. }
          </p>

          <div cdkDropList (cdkDropListDropped)="dropTopItem($event)" class="divide-y divide-slate-100 border-t border-slate-100">
            @for (item of items(); track $index; let i = $index) {
              <div cdkDrag class="py-2">
                <div class="flex items-center gap-2">
                  <span cdkDragHandle class="text-slate-300 cursor-move px-1">⠿</span>
                  <button type="button" (click)="toggleExpand(i)" class="text-slate-400 w-5 shrink-0">{{ expandedIndex() === i ? '▾' : '▸' }}</button>
                  <input class="input flex-1" [(ngModel)]="item.label" placeholder="Label (e.g. Shop)" />
                  <input class="input flex-1" [(ngModel)]="item.url" placeholder="/products or https://…" />
                  <select class="input w-40 text-xs" (change)="pickTarget(item, $event); expandedIndex.set(i)">
                    <option value="">Pick from…</option>
                    <ng-container *ngTemplateOutlet="targetOptions"></ng-container>
                  </select>
                  <button type="button" (click)="removeTopItem(i)" class="text-red-500 px-2">×</button>
                </div>

                @if (expandedIndex() === i) {
                  <div class="ml-9 mt-2 mb-1 p-3 bg-slate-50 rounded-lg">
                    <div class="flex gap-1.5 mb-3">
                      <button type="button" (click)="setMode(item, 'none')" class="text-xs px-2.5 py-1 rounded-md border" [class]="modeOf(item) === 'none' ? 'border-primary bg-primary/5 text-primary' : 'border-slate-200 text-slate-500'">No dropdown</button>
                      <button type="button" (click)="setMode(item, 'simple')" class="text-xs px-2.5 py-1 rounded-md border" [class]="modeOf(item) === 'simple' ? 'border-primary bg-primary/5 text-primary' : 'border-slate-200 text-slate-500'">Simple dropdown</button>
                      <button type="button" (click)="setMode(item, 'mega')" class="text-xs px-2.5 py-1 rounded-md border" [class]="modeOf(item) === 'mega' ? 'border-primary bg-primary/5 text-primary' : 'border-slate-200 text-slate-500'">Mega menu</button>
                    </div>

                    @if (modeOf(item) === 'simple') {
                      <div cdkDropList (cdkDropListDropped)="dropChild(item, $event)" class="space-y-1.5">
                        @for (child of item.children; track $index; let ci = $index) {
                          <div cdkDrag class="flex items-center gap-2">
                            <span cdkDragHandle class="text-slate-300 cursor-move text-xs px-1">⠿</span>
                            <input class="input flex-1 text-sm" [(ngModel)]="child.label" placeholder="Label" />
                            <input class="input flex-1 text-sm" [(ngModel)]="child.url" placeholder="/category/…" />
                            <select class="input w-36 text-xs" (change)="pickTarget(child, $event)">
                              <option value="">Pick from…</option>
                              <ng-container *ngTemplateOutlet="targetOptions"></ng-container>
                            </select>
                            <button type="button" (click)="removeChild(item, ci)" class="text-red-500 px-1.5 text-sm">×</button>
                          </div>
                        }
                      </div>
                      <button type="button" (click)="addChild(item)" class="text-xs text-blue-600 hover:underline mt-2">+ Add link</button>
                    }

                    @if (modeOf(item) === 'mega') {
                      <div cdkDropList (cdkDropListDropped)="dropColumn(item, $event)" class="grid sm:grid-cols-2 gap-3">
                        @for (col of item.megaMenu!.columns; track $index; let colI = $index) {
                          <div cdkDrag class="bg-white border border-slate-200 rounded-lg p-2.5">
                            <div class="flex items-center gap-1.5 mb-2">
                              <span cdkDragHandle class="text-slate-300 cursor-move text-xs">⠿</span>
                              <input class="input flex-1 text-sm font-medium" [(ngModel)]="col.heading" placeholder="Column heading (e.g. Shop by Concern)" />
                              <button type="button" (click)="removeColumn(item, colI)" class="text-red-500 px-1.5 text-sm">×</button>
                            </div>
                            <div cdkDropList (cdkDropListDropped)="dropColumnLink(col, $event)" class="space-y-1 ml-4">
                              @for (link of col.links; track $index; let li = $index) {
                                <div cdkDrag class="flex items-center gap-1.5">
                                  <span cdkDragHandle class="text-slate-300 cursor-move text-xs">⠿</span>
                                  <input class="input flex-1 text-xs" [(ngModel)]="link.label" placeholder="Label" />
                                  <input class="input flex-1 text-xs" [(ngModel)]="link.url" placeholder="URL" />
                                  <select class="input w-32 text-xs" (change)="pickTarget(link, $event)">
                                    <option value="">Pick from…</option>
                                    <ng-container *ngTemplateOutlet="targetOptions"></ng-container>
                                  </select>
                                  <button type="button" (click)="removeColumnLink(col, li)" class="text-red-500 px-1 text-xs">×</button>
                                </div>
                              }
                            </div>
                            <button type="button" (click)="addColumnLink(col)" class="text-xs text-blue-600 hover:underline mt-1.5 ml-4">+ Add link</button>
                          </div>
                        }
                      </div>
                      <button type="button" (click)="addColumn(item)" class="text-xs text-blue-600 hover:underline mt-2">+ Add column</button>

                      <div class="mt-3 pt-3 border-t border-slate-200">
                        <p class="text-xs font-medium text-slate-500 mb-1.5">Promo tile (optional)</p>
                        @if (item.megaMenu!.promo) {
                          <div class="flex items-start gap-2">
                            @if (item.megaMenu!.promo!.imageUrl) {
                              <img [src]="item.megaMenu!.promo!.imageUrl" alt="" class="w-16 h-16 rounded-lg object-cover shrink-0" />
                            }
                            <div class="flex-1 space-y-1.5">
                              <button type="button" (click)="promoPickerFor.set(item)" class="text-xs text-blue-600 hover:underline">{{ item.megaMenu!.promo!.imageUrl ? 'Change image' : 'Choose image' }}</button>
                              <input class="input w-full text-sm" [(ngModel)]="item.megaMenu!.promo!.heading" placeholder="Promo heading" />
                              <input class="input w-full text-sm" [(ngModel)]="item.megaMenu!.promo!.link" placeholder="Link (optional)" />
                            </div>
                            <button type="button" (click)="removePromo(item)" class="text-red-500 px-1.5 text-sm">×</button>
                          </div>
                        } @else {
                          <button type="button" (click)="addPromo(item)" class="text-xs text-blue-600 hover:underline">+ Add promo tile</button>
                        }
                      </div>
                    }
                  </div>
                }
              </div>
            }
            @if (!items().length) { <p class="text-sm text-slate-400 py-3">No items yet.</p> }
          </div>

          <div class="flex gap-2 mt-3">
            <button type="button" (click)="addTopItem()" class="text-sm text-blue-600 hover:underline">+ Add item</button>
            <span class="flex-1"></span>
            <button type="button" (click)="saveMenu()" [disabled]="saving()" class="btn-primary text-sm">{{ saving() ? 'Saving…' : 'Save menu' }}</button>
          </div>
        }
      </div>

      <!-- Redirects -->
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <h2 class="font-semibold text-slate-800 mb-1">URL redirects</h2>
        <p class="text-xs text-slate-400 mb-3">Send visitors from an old path to a new one (e.g. /old-product → /product/new).</p>
        <div class="flex gap-2 mb-3">
          <input class="input flex-1" [(ngModel)]="newFrom" placeholder="/old-path" />
          <span class="self-center text-slate-400">→</span>
          <input class="input flex-1" [(ngModel)]="newTo" placeholder="/new-path" />
          <button type="button" (click)="addRedirect()" class="btn-primary text-sm">Add</button>
        </div>
        <div class="divide-y divide-slate-100">
          @for (r of redirects(); track r.urlRedirectId) {
            <div class="flex items-center justify-between py-2 text-sm">
              <span class="text-slate-600"><span class="font-mono text-slate-800">{{ r.fromPath }}</span> → <span class="font-mono text-slate-800">{{ r.toPath }}</span></span>
              <button type="button" (click)="deleteRedirect(r)" class="text-red-500 hover:underline">Delete</button>
            </div>
          }
          @if (!redirects().length) { <p class="text-sm text-slate-400 py-2">No redirects.</p> }
        </div>
      </div>
    </div>

    <!-- Reusable target picker options: Categories / Collections / Pages, value "type:id" -->
    <ng-template #targetOptions>
      <optgroup label="Categories">
        @for (c of categories(); track c.categoryId) { <option [value]="'category:' + c.categoryId">{{ c.name }}</option> }
      </optgroup>
      <optgroup label="Collections">
        @for (c of collections(); track c.collectionId) { <option [value]="'collection:' + c.collectionId">{{ c.name }}</option> }
      </optgroup>
      <optgroup label="Pages">
        @for (p of pages(); track p.pageId) { <option [value]="'page:' + p.pageId">{{ p.title }}</option> }
      </optgroup>
    </ng-template>

    @if (promoPickerFor()) {
      <app-media-picker (picked)="onPromoPicked($event)" (close)="promoPickerFor.set(null)" />
    }
  `,
})
export class AdminNavigationComponent implements OnInit {
  private readonly api = inject(NavigationAdminService);
  private readonly catalogApi = inject(AdminCatalogService);
  private readonly collectionsApi = inject(CollectionAdminService);
  private readonly cms = inject(CmsService);

  readonly menus = signal<Menu[]>([]);
  readonly active = signal<Menu | null>(null);
  readonly items = signal<MenuItem[]>([]);
  readonly expandedIndex = signal<number | null>(null);
  readonly redirects = signal<UrlRedirect[]>([]);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  newFrom = '';
  newTo = '';

  readonly categories = signal<Category[]>([]);
  readonly collections = signal<AdminCollection[]>([]);
  readonly pages = signal<BuilderPage[]>([]);
  readonly promoPickerFor = signal<MenuItem | null>(null);

  ngOnInit(): void {
    this.api.listMenus().subscribe((m) => { this.menus.set(m); if (m.length) this.pick(m[0]); });
    this.api.listRedirects().subscribe((r) => this.redirects.set(r));
    this.catalogApi.listCategories().subscribe((c) => this.categories.set(c));
    this.collectionsApi.list().subscribe((c) => this.collections.set(c));
    this.cms.listPages().subscribe((p) => this.pages.set(p));
  }

  private toast(m: string): void { this.message.set(m); this.error.set(null); setTimeout(() => this.message.set(null), 2500); }
  private fail(e: unknown): void { this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.'); }

  pick(m: Menu): void {
    this.active.set(m);
    this.items.set(m.items.map((i) => ({ ...i, children: i.children ? i.children.map((c) => ({ ...c })) : i.children })));
    this.expandedIndex.set(null);
  }

  addTopItem(): void { this.items.set([...this.items(), { label: '', url: '' }]); }
  removeTopItem(i: number): void { this.items.set(this.items().filter((_, idx) => idx !== i)); this.expandedIndex.set(null); }
  toggleExpand(i: number): void { this.expandedIndex.set(this.expandedIndex() === i ? null : i); }
  dropTopItem(event: CdkDragDrop<MenuItem[]>): void { moveItemInArray(this.items(), event.previousIndex, event.currentIndex); }

  modeOf(item: MenuItem): Mode {
    if (item.megaMenu) return 'mega';
    if (item.children?.length) return 'simple';
    return 'none';
  }
  setMode(item: MenuItem, mode: Mode): void {
    if (mode === 'none') { item.children = null; item.megaMenu = null; }
    if (mode === 'simple') { item.megaMenu = null; if (!item.children) item.children = []; }
    if (mode === 'mega') { item.children = null; if (!item.megaMenu) item.megaMenu = { columns: [], promo: null }; }
  }

  addChild(item: MenuItem): void { (item.children ??= []).push({ label: '', url: '' }); }
  removeChild(item: MenuItem, i: number): void { item.children = (item.children ?? []).filter((_, idx) => idx !== i); }
  dropChild(item: MenuItem, event: CdkDragDrop<MenuItem[]>): void { if (item.children) moveItemInArray(item.children, event.previousIndex, event.currentIndex); }

  addColumn(item: MenuItem): void { item.megaMenu!.columns.push({ heading: '', links: [] }); }
  removeColumn(item: MenuItem, i: number): void { item.megaMenu!.columns = item.megaMenu!.columns.filter((_, idx) => idx !== i); }
  dropColumn(item: MenuItem, event: CdkDragDrop<MegaMenuColumn[]>): void { moveItemInArray(item.megaMenu!.columns, event.previousIndex, event.currentIndex); }

  addColumnLink(col: MegaMenuColumn): void { col.links.push({ label: '', url: '' }); }
  removeColumnLink(col: MegaMenuColumn, i: number): void { col.links = col.links.filter((_, idx) => idx !== i); }
  dropColumnLink(col: MegaMenuColumn, event: CdkDragDrop<MenuItem[]>): void { moveItemInArray(col.links, event.previousIndex, event.currentIndex); }

  addPromo(item: MenuItem): void { item.megaMenu!.promo = { imageUrl: '', heading: '', link: '' }; }
  removePromo(item: MenuItem): void { item.megaMenu!.promo = null; }
  onPromoPicked(url: string): void {
    const item = this.promoPickerFor();
    if (item?.megaMenu?.promo) item.megaMenu.promo.imageUrl = url;
    this.promoPickerFor.set(null);
  }

  /** Resolves a "type:id" select value against the already-loaded pickers and fills label+url. */
  pickTarget(row: MenuItem, event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    (event.target as HTMLSelectElement).value = '';
    if (!value) return;
    const [type, idStr] = value.split(':');
    const id = +idStr;
    if (type === 'category') {
      const c = this.categories().find((x) => x.categoryId === id);
      if (c) { row.label = c.name; row.url = `/category/${c.slug}`; }
    } else if (type === 'collection') {
      const c = this.collections().find((x) => x.collectionId === id);
      if (c) { row.label = c.name; row.url = `/collection/${c.slug}`; }
    } else if (type === 'page') {
      const p = this.pages().find((x) => x.pageId === id);
      if (p) { row.label = p.title; row.url = `/pages/${p.slug}`; }
    }
  }

  saveMenu(): void {
    const m = this.active(); if (!m) return;
    this.saving.set(true); this.error.set(null);
    const items = this.items().filter((i) => i.label.trim());
    this.api.saveMenu(m.handle, items).subscribe({
      next: () => { this.saving.set(false); this.toast('Menu saved.'); },
      error: (e: unknown) => { this.saving.set(false); this.fail(e); },
    });
  }

  addRedirect(): void {
    if (!this.newFrom.trim() || !this.newTo.trim()) return;
    this.api.createRedirect(this.newFrom.trim(), this.newTo.trim()).subscribe({
      next: (r) => { this.redirects.set([...this.redirects(), r]); this.newFrom = ''; this.newTo = ''; this.toast('Redirect added.'); },
      error: (e: unknown) => this.fail(e),
    });
  }
  deleteRedirect(r: UrlRedirect): void {
    this.api.deleteRedirect(r.urlRedirectId).subscribe({ next: () => this.redirects.set(this.redirects().filter((x) => x.urlRedirectId !== r.urlRedirectId)) });
  }
}
