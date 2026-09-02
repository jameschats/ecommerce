import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ChannelPref, MarketingPlanSettings, NamedDescribedCode, NamedProduct, PosterStudioRequest, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * Poster Studio — a standalone poster editor, separate from the weekly-plan batch flow (per the
 * user's "make it more flexible, like a real editor" ask). Two-phase by design: "Preview" is free and
 * instant (pure SVG render, no AI call) so the merchant can iterate on headline/price/CTA/toggles as
 * much as they like; "Create this poster" is the one credit-metered step (an AI caption for the
 * eventual post), after which the poster can be assigned to any connected channel right here.
 */
@Component({
  selector: 'app-admin-marketing-poster',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Poster Studio</h1>
      <p class="text-sm text-slate-500 mb-6">Craft one poster by hand. Preview freely — nothing is created (or costs credits) until you're happy and click Create.</p>

      <div class="grid lg:grid-cols-2 gap-6">
        <!-- controls -->
        <div class="space-y-5">
          <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
            <div>
              <label class="lbl">Poster type</label>
              <div class="flex gap-2 mt-1">
                <button type="button" (click)="setKind('org')" class="flex-1 text-sm px-3 py-2 rounded-lg border"
                        [class]="kind === 'org' ? 'bg-teal-600 text-white border-teal-600' : 'border-slate-300 text-slate-600 hover:bg-slate-50'">Organization</button>
                <button type="button" (click)="setKind('product')" class="flex-1 text-sm px-3 py-2 rounded-lg border"
                        [class]="kind === 'product' ? 'bg-teal-600 text-white border-teal-600' : 'border-slate-300 text-slate-600 hover:bg-slate-50'">Product</button>
              </div>
            </div>

            @if (kind === 'product') {
              <div>
                <label class="lbl">Product</label>
                <select [(ngModel)]="productId" name="product" class="input" (ngModelChange)="onProductChange()">
                  <option [ngValue]="null">Choose a product…</option>
                  @for (p of products(); track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }
                </select>
              </div>
            }

            <div>
              <label class="lbl">Headline</label>
              <div class="flex gap-2">
                <input [(ngModel)]="headline" name="headline" class="input" placeholder="e.g. Festive elegance is here" (ngModelChange)="schedulePreview()" />
                <button type="button" (click)="suggestHeadline()" [disabled]="suggesting()" class="text-sm px-3 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 shrink-0 disabled:opacity-50">
                  {{ suggesting() ? '…' : '✨ Suggest' }}
                </button>
              </div>
            </div>

            <div class="grid sm:grid-cols-2 gap-4">
              <div>
                <label class="flex items-center gap-2 text-sm text-slate-700 mb-1">
                  <input type="checkbox" [(ngModel)]="showPrice" name="showPrice" (ngModelChange)="schedulePreview()" /> Show price
                </label>
                <input type="number" [(ngModel)]="price" name="price" class="input" [disabled]="!showPrice" placeholder="2499" (ngModelChange)="schedulePreview()" />
              </div>
              <div>
                <label class="lbl">Call to action</label>
                <input [(ngModel)]="cta" name="cta" class="input" placeholder="Shop Now" (ngModelChange)="schedulePreview()" />
              </div>
            </div>

            <div class="flex gap-4 text-sm text-slate-600">
              <label class="flex items-center gap-1.5"><input type="checkbox" [(ngModel)]="includeLogo" name="includeLogo" (ngModelChange)="schedulePreview()" /> Include logo</label>
              <label class="flex items-center gap-1.5"><input type="checkbox" [(ngModel)]="includeName" name="includeName" (ngModelChange)="schedulePreview()" /> Include name</label>
            </div>

            <button type="button" (click)="preview()" [disabled]="previewing()" class="text-sm text-teal-700 hover:underline disabled:opacity-50">
              {{ previewing() ? 'Rendering…' : 'Refresh preview' }}
            </button>
          </section>

          <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
            <div>
              <h2 class="text-sm font-semibold text-slate-800">AI background</h2>
              <p class="text-xs text-slate-500">Swap the plain colour background for a real AI-generated scene — the same engine as Product images. Your headline/price/CTA are still drawn crisply on top by us, not the model, so they always read correctly.</p>
            </div>
            @if (backgroundUrl()) {
              <div class="flex items-center gap-3">
                <img [src]="backgroundUrl()" alt="Generated background" class="w-16 h-16 rounded-lg object-cover border border-slate-200" />
                <div class="text-xs text-slate-500 flex-1">AI background applied.</div>
                <button type="button" (click)="removeBackground()" class="text-xs text-red-600 hover:underline shrink-0">Remove</button>
              </div>
            }
            <div class="flex gap-2">
              <select [(ngModel)]="backgroundStyle" name="bgStyle" class="input flex-1">
                @for (s of backgroundStyles(); track s.key) { <option [ngValue]="s.key">{{ s.label }}</option> }
              </select>
              <button type="button" (click)="generateBackground()" [disabled]="generatingBg() || !canGenerateBackground()"
                      class="text-sm px-3 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 shrink-0 disabled:opacity-50">
                {{ generatingBg() ? 'Generating…' : '✨ Generate (20 credits)' }}
              </button>
            </div>
            @if (bgError()) { <p class="text-xs text-red-600">{{ bgError() }}</p> }
          </section>

          @if (!created()) {
            <button type="button" (click)="create()" [disabled]="creating() || !canCreate()" class="btn-primary w-full disabled:opacity-60">
              {{ creating() ? 'Creating…' : 'Create this poster' }}
            </button>
            <p class="text-xs text-slate-400 text-center">Uses 1 AI credit for the post caption. The image itself is free.</p>
          } @else {
            <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
              <p class="text-sm font-medium text-green-700">✓ Poster created</p>
              <p class="text-sm text-slate-600">{{ created()!.caption }}</p>

              @if (settings(); as s) {
                @if (posterChannels(s).length === 0) {
                  <p class="text-xs text-slate-400">Connect a channel that allows posters to schedule this. <a routerLink="/admin/marketing/connections" class="underline">Connections</a></p>
                } @else if (!scheduled()) {
                  <div class="flex flex-wrap items-center gap-2 pt-1">
                    @for (ch of posterChannels(s); track ch.platform) {
                      <label class="flex items-center gap-1 text-xs text-slate-600 border border-slate-200 rounded-full px-2 py-0.5 cursor-pointer"
                             [class.bg-teal-50]="selected.has(ch.platform)" [class.border-teal-300]="selected.has(ch.platform)">
                        <input type="checkbox" class="sr-only" [checked]="selected.has(ch.platform)" (change)="toggleChannel(ch.platform)" />
                        {{ ch.displayName }}
                      </label>
                    }
                    <button type="button" (click)="scheduleNow()" [disabled]="scheduling() || selected.size === 0" class="text-xs font-medium text-teal-700 hover:underline disabled:opacity-50">
                      {{ scheduling() ? 'Scheduling…' : 'Schedule' }}
                    </button>
                  </div>
                } @else {
                  <p class="text-xs text-green-700">Scheduled — waiting for approval on the <a routerLink="/admin/marketing/scheduler" class="underline">Scheduler</a>.</p>
                }
              }

              <button type="button" (click)="startOver()" class="text-xs text-slate-400 hover:text-slate-600 underline">Make another poster</button>
            </section>
          }
        </div>

        <!-- live preview -->
        <div class="lg:sticky lg:top-6 self-start">
          <div class="bg-white border border-slate-200 rounded-xl p-4 aspect-square flex items-center justify-center overflow-hidden">
            @if (previewUrl()) {
              <img [src]="previewUrl()" alt="Poster preview" class="max-w-full max-h-full rounded-lg" />
            } @else {
              <p class="text-sm text-slate-400">{{ previewing() ? 'Rendering…' : 'Preview will appear here' }}</p>
            }
          </div>
        </div>
      </div>
    </div>
  `,
})
export class AdminMarketingPosterComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);
  private readonly sanitizer = inject(DomSanitizer);
  private previewTimer: ReturnType<typeof setTimeout> | null = null;

  readonly products = signal<NamedProduct[]>([]);
  readonly settings = signal<MarketingPlanSettings | null>(null);
  readonly backgroundStyles = signal<NamedDescribedCode[]>([]);
  readonly backgroundUrl = signal<string | null>(null);
  readonly generatingBg = signal(false);
  readonly bgError = signal<string | null>(null);
  backgroundStyle = 'lifestyle';
  // SVG data URIs aren't on Angular's default image-src allowlist (only base64 raster formats are) —
  // bypassSecurityTrustUrl is safe here: this is our own server-rendered SVG, and a browser never
  // executes scripts embedded in an SVG loaded via <img src>, unlike inline/object/iframe embedding.
  readonly previewUrl = signal<SafeUrl | null>(null);
  readonly previewing = signal(false);
  readonly suggesting = signal(false);
  readonly creating = signal(false);
  readonly created = signal<{ itemId: number; mediaUrl: string; caption: string } | null>(null);
  readonly scheduled = signal(false);
  readonly scheduling = signal(false);
  readonly selected = new Set<string>();

  kind: 'org' | 'product' = 'org';
  productId: number | null = null;
  headline = '';
  showPrice = false;
  price: number | null = null;
  cta = 'Shop Now';
  includeLogo = true;
  includeName = true;

  ngOnInit(): void {
    this.api.videoOptions().subscribe((o) => this.products.set(o.products));
    this.api.getPlanSettings().subscribe((s) => this.settings.set(s));
    this.api.posterBackgroundStyles().subscribe((s) => this.backgroundStyles.set(s));
    this.preview();
  }

  setKind(k: 'org' | 'product'): void { this.kind = k; this.schedulePreview(); }

  onProductChange(): void {
    const p = this.products().find((x) => x.id === this.productId);
    if (p) { this.headline = p.name; this.showPrice = true; }
    this.schedulePreview();
  }

  suggestHeadline(): void {
    this.suggesting.set(true);
    this.api.suggestPosterHeadline(this.kind === 'product' ? this.productId : null, this.headline || null).subscribe({
      next: (r) => { this.headline = r.headline; this.suggesting.set(false); this.preview(); },
      error: () => this.suggesting.set(false),
    });
  }

  schedulePreview(): void {
    if (this.previewTimer) clearTimeout(this.previewTimer);
    this.previewTimer = setTimeout(() => this.preview(), 500);
  }

  private request(): PosterStudioRequest {
    return {
      kind: this.kind, productId: this.kind === 'product' ? this.productId : null,
      headline: this.headline || (this.kind === 'product' ? 'Spotlight' : 'Your store'),
      price: this.showPrice ? this.price : null, cta: this.cta || 'Shop Now',
      includeLogo: this.includeLogo, includeName: this.includeName,
      backgroundImageUrl: this.backgroundUrl(),
    };
  }

  canCreate(): boolean {
    return this.headline.trim().length > 0 && (this.kind === 'org' || this.productId !== null);
  }
  canGenerateBackground(): boolean {
    return this.kind === 'org' || this.productId !== null;
  }

  generateBackground(): void {
    this.generatingBg.set(true);
    this.bgError.set(null);
    this.api.generatePosterBackground(this.request(), this.backgroundStyle).subscribe({
      next: (r) => { this.backgroundUrl.set(r.url); this.generatingBg.set(false); this.preview(); },
      error: () => { this.generatingBg.set(false); this.bgError.set('Could not generate a background. Please try again.'); },
    });
  }

  removeBackground(): void {
    this.backgroundUrl.set(null);
    this.preview();
  }

  preview(): void {
    if (this.kind === 'product' && this.productId === null) return;
    this.previewing.set(true);
    this.api.previewPoster(this.request()).subscribe({
      next: (r) => {
        const url = this.sanitizer.bypassSecurityTrustUrl(`data:image/svg+xml,${encodeURIComponent(r.svg)}`);
        this.previewUrl.set(url);
        this.previewing.set(false);
      },
      error: () => this.previewing.set(false),
    });
  }

  create(): void {
    this.creating.set(true);
    this.api.createPoster(this.request()).subscribe({
      next: (r) => { this.created.set(r); this.creating.set(false); },
      error: () => this.creating.set(false),
    });
  }

  posterChannels(s: MarketingPlanSettings): ChannelPref[] {
    return s.channels.filter((c) => c.enabled && c.allowPoster);
  }

  toggleChannel(platform: string): void {
    if (this.selected.has(platform)) this.selected.delete(platform); else this.selected.add(platform);
  }

  scheduleNow(): void {
    const c = this.created();
    if (!c) return;
    this.scheduling.set(true);
    this.api.scheduleItem(c.itemId, Array.from(this.selected)).subscribe({
      next: () => { this.scheduling.set(false); this.scheduled.set(true); },
      error: () => this.scheduling.set(false),
    });
  }

  startOver(): void {
    this.created.set(null);
    this.scheduled.set(false);
    this.selected.clear();
    this.headline = '';
    this.productId = null;
    this.showPrice = false;
    this.backgroundUrl.set(null);
    this.preview();
  }
}
