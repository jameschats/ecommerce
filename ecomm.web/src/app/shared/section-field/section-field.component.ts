import { Component, OnInit, inject, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { CatalogService } from '../../core/services/catalog.service';
import { CollectionAdminService, AdminCollection } from '../../core/services/collection-admin.service';
import { CmsService, BuilderPage } from '../../core/services/cms.service';
import { NavigationAdminService, Menu } from '../../core/services/navigation-admin.service';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { FieldSchema } from '../../core/services/theme-authoring.service';

type LinkType = 'product' | 'collection' | 'page' | 'external';

/**
 * Renders one section/block settings field from its FieldSchema — the single source of
 * truth for the theme editor's settings form AND its blocks form (previously two separate,
 * inconsistent switches). Two-way bound via [(value)].
 */
@Component({
  selector: 'app-section-field',
  imports: [FormsModule],
  template: `
    @switch (schema().type) {
      @case ('textarea') { <textarea [ngModel]="value()" (ngModelChange)="value.set($event)" rows="3" class="input w-full"></textarea> }
      @case ('richtext') { <textarea [ngModel]="value()" (ngModelChange)="value.set($event)" rows="5" class="input w-full font-mono text-xs" placeholder="<p>HTML — scripts are stripped</p>"></textarea> }
      @case ('boolean') { <input type="checkbox" [ngModel]="value()" (ngModelChange)="value.set($event)" /> }
      @case ('number') { <input type="number" [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full" /> }
      @case ('range') {
        <div class="flex items-center gap-2">
          <input type="range" [ngModel]="value()" (ngModelChange)="value.set($event)" [min]="schema().min ?? 0" [max]="schema().max ?? 100" [step]="schema().step ?? 1" class="w-full" />
          <span class="text-xs text-slate-500 w-8 text-right">{{ value() }}</span>
        </div>
      }
      @case ('color') { <input type="color" [ngModel]="value()" (ngModelChange)="value.set($event)" class="input h-9 w-16" /> }
      @case ('image') { <input [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full" placeholder="https://…/image.jpg" /> }
      @case ('url') { <input type="url" [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full" placeholder="https://…" /> }
      @case ('select') { <select [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full">@for (o of schema().options ?? []; track o) { <option [value]="o">{{ o }}</option> }</select> }

      @case ('category') {
        <select [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full">
          <option value="">—</option>
          @for (c of categories(); track c.categoryId) { <option [value]="c.categoryId">{{ c.name }}</option> }
        </select>
      }
      @case ('collection') {
        <select [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full">
          <option value="">—</option>
          @for (c of collectionsList(); track c.collectionId) { <option [value]="c.collectionId">{{ c.name }}</option> }
        </select>
      }
      @case ('page') {
        <select [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full">
          <option value="">—</option>
          @for (p of pages(); track p.pageId) { <option [value]="p.slug">{{ p.title }}</option> }
        </select>
      }
      @case ('menu') {
        <select [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full">
          @for (m of menus(); track m.handle) { <option [value]="m.handle">{{ m.title }}</option> }
        </select>
      }
      @case ('product') {
        <div>
          <input [(ngModel)]="productQuery" (ngModelChange)="productSearch$.next($event)" class="input w-full" placeholder="Search products…" />
          @if (productResults().length) {
            <div class="border border-slate-200 rounded-lg mt-1 max-h-40 overflow-auto">
              @for (p of productResults(); track p.productId) {
                <button type="button" (click)="pickProduct(p)" class="block w-full text-left px-2 py-1.5 text-sm hover:bg-slate-50">{{ p.name }}</button>
              }
            </div>
          }
          @if (value()) { <p class="text-xs text-slate-500 mt-1">Selected: {{ value() }}</p> }
        </div>
      }
      @case ('link') {
        <div>
          <select [ngModel]="linkType()" (ngModelChange)="setLinkType($event)" class="input w-full mb-1">
            <option value="external">External URL</option>
            <option value="product">Product</option>
            <option value="collection">Collection</option>
            <option value="page">Page</option>
          </select>
          @switch (linkType()) {
            @case ('external') { <input [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full" placeholder="https://… or /products" /> }
            @case ('product') {
              <input [(ngModel)]="productQuery" (ngModelChange)="productSearch$.next($event)" class="input w-full" placeholder="Search products…" />
              @if (productResults().length) {
                <div class="border border-slate-200 rounded-lg mt-1 max-h-40 overflow-auto">
                  @for (p of productResults(); track p.productId) {
                    <button type="button" (click)="pickLinkProduct(p)" class="block w-full text-left px-2 py-1.5 text-sm hover:bg-slate-50">{{ p.name }}</button>
                  }
                </div>
              }
            }
            @case ('collection') {
              <select [ngModel]="linkSlug()" (ngModelChange)="setLink('/collection/' + $event)" class="input w-full">
                <option value="">—</option>
                @for (c of collectionsList(); track c.collectionId) { <option [value]="c.slug">{{ c.name }}</option> }
              </select>
            }
            @case ('page') {
              <select [ngModel]="linkSlug()" (ngModelChange)="setLink('/pages/' + $event)" class="input w-full">
                <option value="">—</option>
                @for (p of pages(); track p.pageId) { <option [value]="p.slug">{{ p.title }}</option> }
              </select>
            }
          }
          @if (value()) { <p class="text-xs text-slate-500 mt-1">{{ value() }}</p> }
        </div>
      }
      @default { <input [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full" /> }
    }
  `,
})
export class SectionFieldComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly collections = inject(CollectionAdminService);
  private readonly cms = inject(CmsService);
  private readonly nav = inject(NavigationAdminService);

  readonly schema = input.required<FieldSchema>();
  readonly value = model<any>();

  readonly categories = signal<Category[]>([]);
  readonly collectionsList = signal<AdminCollection[]>([]);
  readonly pages = signal<BuilderPage[]>([]);
  readonly menus = signal<Menu[]>([]);
  readonly productResults = signal<ProductListItem[]>([]);
  readonly linkType = signal<LinkType>('external');
  /** The slug portion of the current /collection/:slug or /pages/:slug value, for the dropdown. */
  readonly linkSlug = () => {
    const v = String(this.value() ?? '');
    const i = v.indexOf('/', 1);
    return i > 0 ? v.slice(i + 1) : '';
  };

  productQuery = '';
  readonly productSearch$ = new Subject<string>();

  constructor() {
    this.productSearch$.pipe(
      debounceTime(250), distinctUntilChanged(),
      switchMap((q) => this.catalog.getProducts({ search: q.trim(), pageSize: 8 })),
    ).subscribe((r) => this.productResults.set(r.items));
  }

  ngOnInit(): void {
    const t = this.schema().type;
    if (t === 'category') this.catalog.getCategories().subscribe((c) => this.categories.set(c));
    if (t === 'collection' || t === 'link') this.collections.list().subscribe((c) => this.collectionsList.set(c));
    if (t === 'page' || t === 'link') this.cms.listPages().subscribe((p) => this.pages.set(p));
    if (t === 'menu') this.nav.listMenus().subscribe((m) => this.menus.set(m));
    if (t === 'link') {
      const v = String(this.value() ?? '');
      this.linkType.set(
        v.startsWith('/product/') ? 'product' :
        v.startsWith('/collection/') ? 'collection' :
        v.startsWith('/pages/') ? 'page' : 'external');
    }
  }

  pickProduct(p: ProductListItem): void { this.value.set(`/product/${p.slug}`); this.productResults.set([]); this.productQuery = ''; }
  pickLinkProduct(p: ProductListItem): void { this.setLink(`/product/${p.slug}`); this.productResults.set([]); this.productQuery = ''; }
  setLink(v: string): void { this.value.set(v); }
  setLinkType(t: LinkType): void { this.linkType.set(t); if (t === 'external') this.value.set(''); }
}
