import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AiCatalogService, CatalogStatus, GenCategory, GeneratedCatalog } from '../../../core/services/ai-catalog.service';

/**
 * AI-2 sample-catalog generator. Pick a store type (or describe your own) → Generate a preview (25 credits)
 * → review → Add to store / Download as Excel / Regenerate. Seeded products carry AI- SKUs so "Clear sample
 * products" cleanly undoes them. Nothing touches the live catalog until the merchant confirms.
 */
@Component({
  selector: 'app-admin-ai-catalog',
  imports: [FormsModule, RouterLink, CurrencyPipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between gap-3 mb-1">
        <h1 class="text-xl font-bold text-slate-900">Generate a sample catalog</h1>
        <a routerLink="/admin/ai" class="text-sm text-slate-500 hover:underline">AI credits ↗</a>
      </div>
      <p class="text-sm text-slate-500 mb-5">Get a real-looking store in seconds. Pick a store type, generate a preview, then add it to your store — every product is fully editable, and you can clear the samples any time.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (status(); as s) {
        @if (!s.enabled) {
          <div class="rounded-lg bg-amber-50 border border-amber-200 text-amber-800 text-sm px-3 py-2">
            AI features aren't switched on for this platform yet, so catalog generation is unavailable.
          </div>
        } @else {
          @if (s.sampleProducts > 0) {
            <div class="mb-5 rounded-lg bg-slate-50 border border-slate-200 text-slate-600 text-sm px-3 py-2 flex items-center justify-between gap-3">
              <span>You have <span class="font-medium">{{ s.sampleProducts }}</span> AI sample product(s) in your store.</span>
              <button type="button" (click)="clear()" [disabled]="busy()" class="text-red-600 hover:underline whitespace-nowrap">Clear sample products</button>
            </div>
          }

          <!-- Generator form -->
          <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
            <label class="lbl">Store type</label>
            <div class="flex flex-wrap gap-2 mb-3">
              @for (p of s.presets; track p.key) {
                <button type="button" (click)="pick(p.key)"
                        class="text-sm px-3 py-1.5 rounded-full border"
                        [class]="form.presetKey === p.key ? 'border-violet-400 bg-violet-50 text-violet-700' : 'border-slate-300 text-slate-600 hover:bg-slate-50'">
                  {{ p.label }}
                </button>
              }
              <button type="button" (click)="pick(null)"
                      class="text-sm px-3 py-1.5 rounded-full border"
                      [class]="form.presetKey === null ? 'border-violet-400 bg-violet-50 text-violet-700' : 'border-slate-300 text-slate-600 hover:bg-slate-50'">
                Describe my own…
              </button>
            </div>

            @if (form.presetKey === null) {
              <textarea [(ngModel)]="form.prompt" name="prompt" rows="2" class="input mb-3"
                        placeholder="e.g. an organic tea and coffee shop with loose-leaf teas, brewing kit and gift boxes"></textarea>
            }

            <div class="flex flex-wrap items-end gap-4">
              <div>
                <label class="lbl">Categories</label>
                <input type="number" min="1" max="12" [(ngModel)]="form.categories" name="cats" class="input w-24" />
              </div>
              <div>
                <label class="lbl">Products / category</label>
                <input type="number" min="1" max="12" [(ngModel)]="form.productsPerCategory" name="per" class="input w-28" />
              </div>
              <button type="button" (click)="generate()" [disabled]="busy() || !canGenerate()" class="btn-primary px-5 py-2.5">
                {{ generating() ? 'Generating…' : '✨ Generate (25 credits)' }}
              </button>
            </div>
          </div>

          <!-- Preview -->
          @if (preview(); as pv) {
            <div class="flex items-center justify-between gap-3 mb-3">
              <h2 class="font-semibold text-slate-800">{{ pv.storeType }} — {{ productCount() }} products</h2>
              <div class="flex gap-2">
                <button type="button" (click)="add()" [disabled]="busy()" class="btn-primary px-4 py-2 text-sm">{{ seeding() ? 'Adding…' : 'Add to my store' }}</button>
                <button type="button" (click)="download()" [disabled]="busy()" class="px-4 py-2 text-sm rounded-lg border border-slate-300 hover:bg-slate-50">Download Excel</button>
                <button type="button" (click)="generate()" [disabled]="busy()" class="px-4 py-2 text-sm rounded-lg border border-slate-300 hover:bg-slate-50">Regenerate</button>
              </div>
            </div>
            <p class="text-xs text-slate-400 mb-4">Preview only — nothing is added until you click “Add to my store”. Photos are curated stock; swap them per product after adding.</p>

            @for (cat of pv.categories; track cat.name) {
              <div class="mb-6">
                <h3 class="font-medium text-slate-800">{{ cat.name }}</h3>
                @if (cat.description) { <p class="text-xs text-slate-400 mb-2">{{ cat.description }}</p> }
                @for (leaf of leavesOf(cat); track leaf.name) {
                  @if (leaf !== cat) { <div class="text-xs font-medium text-slate-500 mt-2 mb-1">{{ leaf.name }}</div> }
                  <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
                    @for (p of leaf.products; track p.name) {
                      <div class="border border-slate-200 rounded-lg overflow-hidden bg-white">
                        <img [src]="p.imageUrl" alt="" class="h-28 w-full object-cover" loading="lazy" />
                        <div class="p-2.5">
                          <div class="text-sm font-medium text-slate-800 leading-snug">{{ p.name }}</div>
                          <div class="text-sm text-slate-900 mt-0.5">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</div>
                          <p class="text-xs text-slate-400 mt-1 line-clamp-2">{{ p.shortDescription }}</p>
                          @if (p.attributes) {
                            <div class="flex flex-wrap gap-1 mt-1.5">
                              @for (a of attrEntries(p.attributes); track a[0]) {
                                <span class="text-[11px] px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">{{ a[0] }}: {{ a[1] }}</span>
                              }
                            </div>
                          }
                        </div>
                      </div>
                    }
                  </div>
                }
              </div>
            }
          }
        }
      }
    </div>
  `,
})
export class AdminAiCatalogComponent implements OnInit {
  private readonly api = inject(AiCatalogService);
  private readonly route = inject(ActivatedRoute);

  readonly status = signal<CatalogStatus | null>(null);
  readonly generating = signal(false);
  readonly seeding = signal(false);
  readonly clearing = signal(false);
  readonly busy = computed(() => this.generating() || this.seeding() || this.clearing());
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly preview = signal<GeneratedCatalog | null>(null);

  form: { presetKey: string | null; prompt: string; categories: number; productsPerCategory: number } = {
    presetKey: 'bazaar', prompt: '', categories: 6, productsPerCategory: 6,
  };

  readonly productCount = computed(() => {
    const pv = this.preview();
    if (!pv) return 0;
    let n = 0;
    for (const c of pv.categories) for (const leaf of this.leavesOf(c)) n += leaf.products.length;
    return n;
  });

  ngOnInit(): void {
    const preset = this.route.snapshot.queryParamMap.get('preset');
    if (preset) this.form.presetKey = preset;
    this.load();
  }

  private load(): void {
    this.api.status().subscribe({ next: (s) => this.status.set(s), error: () => {} });
  }

  /** Leaf categories that actually hold products (the category itself when it has no subcategories). */
  leavesOf(cat: GenCategory): GenCategory[] {
    return cat.subcategories && cat.subcategories.length ? cat.subcategories : [cat];
  }

  attrEntries(attrs: Record<string, string>): [string, string][] { return Object.entries(attrs); }

  pick(key: string | null): void { this.form.presetKey = key; }
  canGenerate(): boolean { return this.form.presetKey !== null || this.form.prompt.trim().length > 0; }

  private flash(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 3500); }

  generate(): void {
    this.generating.set(true); this.error.set(null); this.message.set(null);
    this.api.generate({
      presetKey: this.form.presetKey, prompt: this.form.presetKey === null ? this.form.prompt : null,
      categories: this.form.categories, productsPerCategory: this.form.productsPerCategory,
    }).subscribe({
      next: (c) => { this.preview.set(c); this.generating.set(false); },
      error: (e: unknown) => { this.generating.set(false); this.error.set(this.msg(e, true)); },
    });
  }

  add(): void {
    const pv = this.preview();
    if (!pv) return;
    this.seeding.set(true); this.error.set(null);
    this.api.seed(pv).subscribe({
      next: (r) => { this.seeding.set(false); this.preview.set(null); this.flash(`Added ${r.products} products in ${r.categories} categories. Edit them under Products.`); this.load(); },
      error: (e: unknown) => { this.seeding.set(false); this.error.set(this.msg(e)); },
    });
  }

  download(): void {
    const pv = this.preview();
    if (!pv || typeof document === 'undefined') return;
    this.api.export(pv).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = 'sample-catalog.xlsx'; a.click();
      URL.revokeObjectURL(url);
    });
  }

  clear(): void {
    if (typeof window !== 'undefined' && !window.confirm('Remove all AI sample products from your store? This cannot be undone.')) return;
    this.clearing.set(true); this.error.set(null);
    this.api.clear().subscribe({
      next: (r) => { this.clearing.set(false); this.flash(`Removed ${r.removed} sample product(s).`); this.load(); },
      error: (e: unknown) => { this.clearing.set(false); this.error.set(this.msg(e)); },
    });
  }

  private msg(e: unknown, credits = false): string {
    const err = e as { status?: number; error?: { message?: string } };
    if (credits && err?.status === 402) return err.error?.message ?? 'Not enough AI credits — top up on the AI credits page.';
    return err?.error?.message ?? 'Something went wrong. Please try again.';
  }
}
