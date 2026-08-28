import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';
import { ProductListItem } from '../../../core/models/catalog.model';
import { GenerateRequest, GrowthContent, GrowthService, GrowthType } from '../../../core/services/growth.service';

@Component({
  selector: 'app-admin-growth-generate',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 class="text-xl font-bold text-slate-900">✨ Generate marketing content</h1>
          <p class="text-sm text-slate-500">Turn a product into a caption, post, email or offer in seconds.</p>
        </div>
        <a routerLink="/admin/growth/library" class="text-sm text-primary hover:underline shrink-0">Content library →</a>
      </div>

      @if (locked()) {
        <div class="rounded-xl border border-violet-200 bg-violet-50 p-6 text-center">
          <div class="text-3xl">✨</div>
          <h2 class="font-semibold text-violet-900 mt-2">AI Marketing is a plan upgrade away</h2>
          <p class="text-sm text-violet-700/80 mt-1">Generate captions, posts, emails and offers from your products.</p>
          <a routerLink="/admin/billing" class="inline-block mt-3 btn-primary">See plans</a>
        </div>
      } @else {
        <!-- Type picker -->
        <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-3 mb-6">
          @for (t of types(); track t.key) {
            <button type="button" (click)="pick(t)"
                    class="text-left rounded-xl border p-3 transition"
                    [class]="selected()?.key === t.key ? 'border-primary ring-1 ring-primary/20 bg-blue-50/40' : 'border-slate-200 hover:border-slate-300'">
              <div class="flex items-center justify-between">
                <span class="text-sm font-medium text-slate-800">{{ t.label }}</span>
                <span class="text-[11px] text-slate-400">{{ t.credits }} cr</span>
              </div>
              <div class="text-xs text-slate-500 mt-0.5">{{ t.description }}</div>
            </button>
          }
        </div>

        @if (selected(); as t) {
          <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
            @if (t.needsProduct) {
              <label class="lbl">Product</label>
              <select [(ngModel)]="productId" name="product" class="input">
                <option [ngValue]="null" disabled>Choose a product…</option>
                @for (p of products(); track p.productId) { <option [ngValue]="p.productId">{{ p.name }}</option> }
              </select>
            }
            <div [class.mt-3]="t.needsProduct">
              <label class="lbl">
                {{ t.needsProduct ? 'Extra instructions (optional)' : 'What is the offer / occasion?' }}
              </label>
              <textarea [(ngModel)]="brief" name="brief" rows="2" class="input"
                        [placeholder]="t.needsProduct ? 'e.g. mention free delivery' : 'e.g. Pongal sale — flat 15% off'"></textarea>
            </div>
            <div class="mt-3">
              <label class="lbl">Language</label>
              <select [(ngModel)]="language" name="lang" class="input w-auto">
                <option [ngValue]="null">Brand default</option>
                @for (l of languages; track l) { <option [ngValue]="l">{{ l }}</option> }
              </select>
            </div>

            @if (error()) { <p class="text-sm text-red-600 mt-3">{{ error() }}</p> }
            <div class="flex flex-wrap items-center gap-3 mt-4">
              <button type="button" (click)="generate(t)" [disabled]="generating() || !ready(t)"
                      class="btn-primary disabled:opacity-60">
                {{ generating() ? 'Generating…' : 'Generate (' + t.credits + ' credits)' }}
              </button>
              @if (t.needsProduct) {
                <button type="button" (click)="bulkMode.set(!bulkMode())" class="text-sm text-primary hover:underline">
                  {{ bulkMode() ? '← Single product' : 'Generate for many products →' }}
                </button>
              }
            </div>

            @if (t.needsProduct && bulkMode()) {
              <div class="mt-4 border-t border-slate-100 pt-4">
                <label class="lbl">Pick products (up to 50) — one {{ t.label.toLowerCase() }} each, generated in the background</label>
                <select multiple size="6" [(ngModel)]="bulkIds" name="bulkIds" class="input h-auto">
                  @for (p of products(); track p.productId) { <option [ngValue]="p.productId">{{ p.name }}</option> }
                </select>
                <div class="flex items-center gap-3 mt-3">
                  <button type="button" (click)="bulkGenerate(t)" [disabled]="bulkBusy() || bulkIds.length === 0"
                          class="btn-primary disabled:opacity-60">
                    {{ bulkBusy() ? 'Queuing…' : 'Generate for ' + bulkIds.length + ' product(s) (' + (bulkIds.length * t.credits) + ' cr)' }}
                  </button>
                  @if (bulkMsg()) { <span class="text-sm text-emerald-700">{{ bulkMsg() }}</span> }
                </div>
              </div>
            }
          </div>
        }

        <!-- Result -->
        @if (result(); as r) {
          <div class="bg-white border border-emerald-200 rounded-xl p-5">
            <div class="flex items-center justify-between mb-3">
              <h2 class="font-semibold text-slate-800">Your draft</h2>
              <span class="text-xs text-slate-400">Saved to your library</span>
            </div>
            @if (r.title !== null) {
              <label class="lbl">Subject</label>
              <input [(ngModel)]="editTitle" name="editTitle" class="input mb-3" />
            }
            <textarea [(ngModel)]="editBody" name="editBody" rows="8" class="input font-normal"></textarea>
            <div class="flex flex-wrap items-center gap-2 mt-3">
              <button type="button" (click)="copy()" class="btn-primary">{{ copied() ? 'Copied ✓' : 'Copy' }}</button>
              <button type="button" (click)="save(r, 'Kept')" class="btn-ghost border border-slate-300">Save edits</button>
              <button type="button" (click)="regenerate()" class="btn-ghost border border-slate-300">Regenerate</button>
            </div>
          </div>
        }
      }
    </div>
  `,
})
export class AdminGrowthGenerateComponent implements OnInit {
  private readonly api = inject(GrowthService);
  private readonly catalog = inject(AdminCatalogService);

  readonly types = signal<GrowthType[]>([]);
  readonly products = signal<ProductListItem[]>([]);
  readonly selected = signal<GrowthType | null>(null);
  readonly result = signal<GrowthContent | null>(null);
  readonly generating = signal(false);
  readonly locked = signal(false);
  readonly copied = signal(false);
  readonly error = signal<string | null>(null);

  productId: number | null = null;
  brief = '';
  language: string | null = null;
  editBody = '';
  editTitle = '';

  // Bulk generate (M1)
  readonly bulkMode = signal(false);
  readonly bulkBusy = signal(false);
  readonly bulkMsg = signal<string | null>(null);
  bulkIds: number[] = [];

  readonly languages = ['English', 'Hindi', 'Tamil', 'Telugu', 'Hinglish'];

  ngOnInit(): void {
    this.api.types().subscribe({
      next: (t) => this.types.set(t),
      error: (e) => { if (e?.status === 402) this.locked.set(true); },
    });
    this.catalog.listProducts({ page: 1, pageSize: 200 }).subscribe((r) => this.products.set(r.items));
  }

  pick(t: GrowthType): void {
    this.selected.set(t);
    this.error.set(null);
  }

  ready(t: GrowthType): boolean {
    return t.needsProduct ? this.productId !== null : this.brief.trim().length > 0;
  }

  generate(t: GrowthType): void {
    this.generating.set(true);
    this.error.set(null);
    this.copied.set(false);
    const req: GenerateRequest = {
      contentType: t.key,
      productId: t.needsProduct ? this.productId : null,
      language: this.language,
      brief: this.brief.trim() || null,
    };
    this.api.generate(req).subscribe({
      next: (r) => {
        this.result.set(r);
        this.editBody = r.body;
        this.editTitle = r.title ?? '';
        this.generating.set(false);
      },
      error: (e) => {
        this.error.set(e?.status === 402
          ? "You're out of AI credits — top up under AI credits to keep generating."
          : e?.error?.message ?? 'Could not generate that just now.');
        this.generating.set(false);
      },
    });
  }

  regenerate(): void {
    const t = this.selected();
    if (t) this.generate(t);
  }

  bulkGenerate(t: GrowthType): void {
    if (this.bulkIds.length === 0) return;
    this.bulkBusy.set(true);
    this.bulkMsg.set(null);
    this.error.set(null);
    this.api.bulkGenerate(t.key, this.bulkIds, this.language, this.brief.trim() || null).subscribe({
      next: (r) => { this.bulkBusy.set(false); this.bulkMsg.set(r.message); this.bulkIds = []; },
      error: (e) => {
        this.bulkBusy.set(false);
        this.error.set(e?.status === 402 ? "You're out of AI credits — top up to keep generating." : e?.error?.message ?? 'Could not queue the batch.');
      },
    });
  }

  save(r: GrowthContent, status: string): void {
    this.api.update(r.id, this.editBody, this.editTitle || null, status).subscribe((updated) => this.result.set(updated));
  }

  copy(): void {
    const text = (this.editTitle ? this.editTitle + '\n\n' : '') + this.editBody;
    navigator.clipboard?.writeText(text).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }
}
