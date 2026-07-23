import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';
import { ProductListItem } from '../../../core/models/catalog.model';
import { GeneratedImage, GrowthService, ImageFormat, ImageStyle } from '../../../core/services/growth.service';

@Component({
  selector: 'app-admin-growth-images',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-start justify-between gap-4 mb-2">
        <h1 class="text-xl font-bold text-slate-900">🖼️ Product images <span class="text-[11px] align-middle px-1.5 py-0.5 rounded-full bg-amber-100 text-amber-700">Beta</span></h1>
        <a routerLink="/admin/growth" class="text-sm text-primary hover:underline shrink-0">Text content →</a>
      </div>
      <p class="text-sm text-slate-500 mb-6">
        Generates a fresh marketing image from your product's details. It's an AI impression for posters and
        social — not a photo of your exact item. Each image uses more credits than text.
      </p>

      @if (locked()) {
        <div class="rounded-xl border border-violet-200 bg-violet-50 p-6 text-center">
          <div class="text-3xl">🖼️</div>
          <h2 class="font-semibold text-violet-900 mt-2">Image generation is a plan upgrade away</h2>
          <a routerLink="/admin/billing" class="inline-block mt-3 btn-primary">See plans</a>
        </div>
      } @else if (!enabled()) {
        <div class="rounded-xl border border-slate-200 bg-slate-50 p-6 text-center text-sm text-slate-500">
          Image generation isn't switched on for this store yet.
        </div>
      } @else {
        <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
          <div class="grid sm:grid-cols-2 gap-4">
            <div>
              <label class="lbl">Product</label>
              <select [(ngModel)]="productId" name="product" class="input">
                <option [ngValue]="null" disabled>Choose a product…</option>
                @for (p of products(); track p.productId) { <option [ngValue]="p.productId">{{ p.name }}</option> }
              </select>
            </div>
            <div>
              <label class="lbl">Style</label>
              <select [(ngModel)]="style" name="style" class="input">
                @for (s of styles(); track s.key) { <option [ngValue]="s.key">{{ s.label }}</option> }
              </select>
            </div>
          </div>
          <div class="mt-3">
            <label class="lbl">Format</label>
            <select [(ngModel)]="format" name="format" class="input">
              @for (f of formats(); track f.key) { <option [ngValue]="f.key">{{ f.label }}</option> }
            </select>
          </div>
          <div class="mt-3">
            <label class="lbl">Anything to add? <span class="text-slate-400 font-normal">(optional)</span></label>
            <input [(ngModel)]="brief" name="brief" class="input" placeholder="e.g. Diwali gifting, warm tones" />
          </div>

          @if (error()) { <p class="text-sm text-red-600 mt-3">{{ error() }}</p> }
          <button type="button" (click)="generate()" [disabled]="generating() || productId === null || !style"
                  class="btn-primary mt-4 disabled:opacity-60">
            {{ generating() ? 'Generating… (up to a minute)' : 'Generate image (20 credits)' }}
          </button>
        </div>

        @if (result(); as r) {
          <div class="bg-white border border-emerald-200 rounded-xl p-5 mb-6">
            <img [src]="src(r.url)" alt="Generated product image" class="max-w-full rounded-lg border border-slate-200" />
            <div class="flex items-center gap-3 mt-3">
              <a [href]="src(r.url)" download class="btn-primary">Download</a>
              <span class="text-xs text-slate-400">Cost to us: ₹{{ r.costInr | number:'1.2-2' }}</span>
            </div>
          </div>
        }

        @if ((recent()?.length ?? 0) > 0) {
          <h2 class="font-semibold text-slate-800 mb-2">Recent images</h2>
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
            @for (img of recent() ?? []; track img.id) {
              <a [href]="src(img.url)" target="_blank" rel="noopener" [title]="img.createdAt | date:'dd MMM, HH:mm'">
                <img [src]="src(img.url)" alt="" class="w-full aspect-square object-cover rounded-lg border border-slate-200 hover:border-slate-300" />
              </a>
            }
          </div>
        }
      }
    </div>
  `,
})
export class AdminGrowthImagesComponent implements OnInit {
  private readonly api = inject(GrowthService);
  private readonly catalog = inject(AdminCatalogService);

  readonly styles = signal<ImageStyle[]>([]);
  readonly formats = signal<ImageFormat[]>([]);
  readonly products = signal<ProductListItem[]>([]);
  readonly result = signal<GeneratedImage | null>(null);
  readonly recent = signal<GeneratedImage[] | null>(null);
  readonly generating = signal(false);
  readonly locked = signal(false);
  readonly enabled = signal(true);
  readonly error = signal<string | null>(null);

  productId: number | null = null;
  style = '';
  format = '';
  brief = '';

  ngOnInit(): void {
    this.api.imageStyles().subscribe({
      next: (s) => { this.styles.set(s); if (s.length) this.style = s[0].key; },
      error: (e) => { if (e?.status === 402) this.locked.set(true); },
    });
    this.api.imageFormats().subscribe((f) => { this.formats.set(f); if (f.length) this.format = f[0].key; });
    this.catalog.listProducts({ page: 1, pageSize: 200 }).subscribe((r) => this.products.set(r.items));
    this.loadRecent();
  }

  /** Stored image URLs are absolute (they include the API/media host). */
  src(url: string): string { return url; }

  generate(): void {
    if (this.productId === null || !this.style) return;
    this.generating.set(true);
    this.error.set(null);
    this.api.generateImage(this.productId, this.style, this.format || null, this.brief.trim() || null).subscribe({
      next: (img) => { this.result.set(img); this.generating.set(false); this.loadRecent(); },
      error: (e) => {
        this.error.set(
          e?.status === 402 ? "You're out of AI credits — top up to generate images."
          : e?.status === 503 ? "Image generation isn't switched on for this store yet."
          : e?.error?.message ?? 'Could not generate that image.');
        this.generating.set(false);
        if (e?.status === 503) this.enabled.set(false);
      },
    });
  }

  private loadRecent(): void {
    this.api.recentImages().subscribe({ next: (r) => this.recent.set(r), error: () => {} });
  }
}
