import { Component, OnInit, computed, inject, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { CatalogService } from '../../core/services/catalog.service';
import { CollectionAdminService, AdminCollection } from '../../core/services/collection-admin.service';
import { CmsService, BuilderPage } from '../../core/services/cms.service';
import { NavigationAdminService, Menu } from '../../core/services/navigation-admin.service';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { FieldSchema } from '../../core/services/theme-authoring.service';
import { ColorScheme } from '../../core/services/theme.service';
import { AiAssistButtonComponent } from '../ai-assist/ai-assist-button.component';
import { MediaPickerComponent } from '../media-picker/media-picker.component';

type LinkType = 'product' | 'collection' | 'page' | 'external';

/**
 * Renders one section/block settings field from its FieldSchema — the single source of
 * truth for the theme editor's settings form AND its blocks form (previously two separate,
 * inconsistent switches). Two-way bound via [(value)].
 */
@Component({
  selector: 'app-section-field',
  imports: [FormsModule, AiAssistButtonComponent, MediaPickerComponent],
  template: `
    @switch (schema().type) {
      @case ('textarea') {
        <textarea [ngModel]="value()" (ngModelChange)="value.set($event)" rows="3" class="input w-full"></textarea>
        <app-ai-assist purpose="theme-section-text" [text]="value() || ''" (applied)="value.set($event)" />
      }
      @case ('richtext') {
        <textarea [ngModel]="value()" (ngModelChange)="value.set($event)" rows="5" class="input w-full font-mono text-xs" placeholder="<p>HTML — scripts are stripped</p>"></textarea>
        <app-ai-assist purpose="theme-section-text" [text]="value() || ''" (applied)="value.set($event)" />
      }
      @case ('boolean') { <input type="checkbox" [ngModel]="value()" (ngModelChange)="value.set($event)" /> }
      @case ('number') { <input type="number" [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full" /> }
      @case ('range') {
        <div class="flex items-center gap-2">
          <input type="range" [ngModel]="value()" (ngModelChange)="value.set($event)" [min]="schema().min ?? 0" [max]="schema().max ?? 100" [step]="schema().step ?? 1" class="w-full" />
          <span class="text-xs text-slate-500 w-8 text-right">{{ value() }}</span>
        </div>
      }
      @case ('color') { <input type="color" [ngModel]="value()" (ngModelChange)="value.set($event)" class="input h-9 w-16" /> }
      @case ('image') {
        <div>
          @if (value()) {
            <div class="relative w-full aspect-video rounded-lg overflow-hidden border border-slate-200 mb-1.5 bg-slate-50">
              <img [src]="value()" alt="" class="w-full h-full object-cover" />
              <button type="button" (click)="value.set('')" aria-label="Remove image"
                class="absolute top-1 right-1 w-6 h-6 rounded-full bg-white/90 hover:bg-white text-slate-500 hover:text-slate-800 text-sm grid place-items-center">×</button>
            </div>
          }
          <button type="button" (click)="mediaPickerOpen.set(true)" class="input w-full text-left text-slate-500 hover:bg-slate-50">
            {{ value() ? 'Change image' : 'Choose image…' }}
          </button>
        </div>
        @if (mediaPickerOpen()) {
          <app-media-picker (picked)="value.set($event); mediaPickerOpen.set(false)" (close)="mediaPickerOpen.set(false)" />
        }
      }
      @case ('url') { <input type="url" [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full" placeholder="https://…" /> }
      @case ('datetime') { <input type="datetime-local" [ngModel]="toLocalInput(value())" (ngModelChange)="value.set(toUtcIso($event))" class="input w-full" /> }
      @case ('select') {
        @if (segmentedOptions(); as opts) {
          <div class="flex rounded-lg border border-slate-300 overflow-hidden">
            @for (o of opts; track o) {
              <button type="button" (click)="value.set(o)" class="flex-1 text-sm px-2 py-1.5 transition" [class]="value() === o ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">{{ o }}</button>
            }
          </div>
        } @else {
          <select [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full">@for (o of schema().options ?? []; track o) { <option [value]="o">{{ o }}</option> }</select>
        }
      }

      @case ('colorScheme') {
        <select [ngModel]="value()" (ngModelChange)="value.set($event)" class="input w-full">
          <option value="">—</option>
          @for (s of colorSchemes(); track s.key) { <option [value]="s.key">{{ s.name }}</option> }
        </select>
      }
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
          @if (value()) {
            <div class="mt-1.5 inline-flex items-center gap-1.5 text-xs bg-slate-100 border border-slate-200 rounded-full pl-2.5 pr-1.5 py-1">
              <span class="text-slate-400">🔗</span>
              <span class="font-medium text-slate-700">{{ linkChipLabel() }}</span>
              <button type="button" (click)="setLink(''); linkedProductName.set(null)" aria-label="Remove link" class="text-slate-400 hover:text-slate-700 ml-0.5 leading-none">×</button>
            </div>
          }
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
  /** The current theme's named colour schemes — theme-scoped data the parent editor already
   *  has loaded, not a fetchable catalog resource like the other pickers. */
  readonly colorSchemes = input<ColorScheme[]>([]);

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

  /** E4: a picked/looked-up product name for the link chip — the stored value is only a slug, so a
   *  freshly-loaded field (not just-picked in this session) needs one lookup to show a real name. */
  readonly linkedProductName = signal<string | null>(null);
  readonly linkChipLabel = computed(() => {
    const v = String(this.value() ?? '');
    if (v.startsWith('/product/')) return `Product: ${this.linkedProductName() ?? v.slice(9)}`;
    if (v.startsWith('/collection/')) { const slug = v.slice(12); return `Collection: ${this.collectionsList().find((c) => c.slug === slug)?.name ?? slug}`; }
    if (v.startsWith('/pages/')) { const slug = v.slice(7); return `Page: ${this.pages().find((p) => p.slug === slug)?.title ?? slug}`; }
    return v;
  });

  /** E4: select fields with a handful of options render as a segmented control instead of a dropdown. */
  readonly segmentedOptions = computed(() => {
    const opts = this.schema().options ?? [];
    return opts.length > 0 && opts.length <= 4 ? opts : null;
  });

  /** E4: image field's media-library/upload/URL picker modal. */
  readonly mediaPickerOpen = signal(false);

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
      // E4: resolve the chip's display name for an already-saved product link (a fresh pick sets
      // linkedProductName directly and skips this network round-trip).
      if (v.startsWith('/product/')) {
        this.catalog.getProductBySlug(v.slice(9)).subscribe((p) => { if (p) this.linkedProductName.set(p.name); });
      }
    }
  }

  pickProduct(p: ProductListItem): void { this.value.set(`/product/${p.slug}`); this.productResults.set([]); this.productQuery = ''; }
  pickLinkProduct(p: ProductListItem): void { this.setLink(`/product/${p.slug}`); this.linkedProductName.set(p.name); this.productResults.set([]); this.productQuery = ''; }
  setLink(v: string): void { this.value.set(v); }
  setLinkType(t: LinkType): void { this.linkType.set(t); if (t === 'external') this.value.set(''); }

  /** Stored value is a UTC ISO string; <input type="datetime-local"> needs local "YYYY-MM-DDTHH:mm". */
  toLocalInput(utcIso: unknown): string {
    if (!utcIso || typeof utcIso !== 'string') return '';
    const d = new Date(utcIso);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
  /** Reverse: the input's local "YYYY-MM-DDTHH:mm" back to a UTC ISO string for storage — this is what
   *  makes the countdown's target moment the same instant for every shopper regardless of their timezone. */
  toUtcIso(localValue: string): string {
    const d = new Date(localValue);
    return isNaN(d.getTime()) ? '' : d.toISOString();
  }
}
