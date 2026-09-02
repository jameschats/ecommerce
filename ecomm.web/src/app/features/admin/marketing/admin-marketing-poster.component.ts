import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import {
  ChannelPref, MarketingBrand, MarketingPlanSettings, NamedProduct, PosterEditorOptions,
  PosterStudioRequest, PosterTemplateInfo, MarketingStudioService,
} from '../../../core/services/marketing-studio.service';

/**
 * Poster Studio — a Canva-style Browse (category-grouped template gallery) that opens into an Editor.
 * Two-phase by design to protect credits: Preview is free/instant (pure SVG render, no AI call) so the
 * merchant can iterate freely; Create is the one metered step (an AI caption). Reopening a saved poster
 * (via ?edit=<creativeId>) or starting from a template card (via ?template=<id>) both land in the same
 * Editor — saving an edit re-renders in place for free, it never re-spends the caption credit.
 */
@Component({
  selector: 'app-admin-marketing-poster',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-6xl mx-auto p-6">
      @if (mode() === 'browse') {
        <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Poster Studio</h1>
        <p class="text-sm text-slate-500 mb-6">Pick a template to start. Preview freely — nothing is created (or costs credits) until you save.</p>

        @if (opts(); as o) {
          @for (cat of categories(o); track cat) {
            <div class="mb-7">
              <h2 class="text-sm font-semibold text-slate-700 mb-2.5">{{ cat }}</h2>
              <div class="grid sm:grid-cols-3 gap-4">
                @for (t of templatesIn(o, cat); track t.id) {
                  <button type="button" (click)="startFromTemplate(t.id)"
                          class="text-left bg-white border border-slate-200 rounded-xl overflow-hidden hover:border-teal-400 hover:shadow-sm transition-all">
                    <div class="aspect-square bg-slate-100 flex items-center justify-center">
                      @if (thumb(t.id); as svg) {
                        <img [src]="svg" alt="" class="w-full h-full object-cover" />
                      } @else {
                        <span class="text-xs text-slate-400">Loading…</span>
                      }
                    </div>
                    <div class="p-3">
                      <div class="text-sm font-semibold text-slate-800">{{ t.name }}</div>
                      <div class="text-xs text-slate-500 mt-0.5 line-clamp-2">{{ t.description }}</div>
                      @if (!t.usesPhoto) { <div class="text-xs text-teal-700 mt-1 font-medium">No photo needed</div> }
                    </div>
                  </button>
                }
              </div>
            </div>
          }
        } @else {
          <p class="text-sm text-slate-500">Loading…</p>
        }
      } @else if (opts(); as o) {
        <div class="flex items-center gap-2 mb-1">
          <button type="button" (click)="backToBrowse()" class="text-sm text-slate-500 hover:text-slate-700">← Templates</button>
        </div>
        <h1 class="text-xl font-bold text-slate-900 mb-6">{{ editCreativeId() ? 'Edit poster' : 'New poster' }}</h1>

        <div class="grid lg:grid-cols-2 gap-6">
          <!-- controls -->
          <div class="space-y-5">
            <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
              <label class="lbl">Template</label>
              <div class="grid sm:grid-cols-2 gap-3">
                @for (t of o.templates; track t.id) {
                  <button type="button" (click)="setTemplate(t.id)"
                          class="text-left rounded-lg border-2 p-3 transition-colors"
                          [class]="templateId === t.id ? 'border-teal-500 bg-teal-50' : 'border-slate-200 hover:border-slate-300'">
                    <div class="text-sm font-semibold text-slate-800">{{ t.name }}</div>
                    <div class="text-xs text-slate-500 mt-0.5">{{ t.description }}</div>
                  </button>
                }
              </div>

              <label class="lbl pt-1">Format</label>
              <div class="flex gap-2">
                @for (f of o.formats; track f.id) {
                  <button type="button" (click)="setFormat(f.id)" class="flex-1 text-xs px-3 py-2 rounded-lg border"
                          [class]="format === f.id ? 'bg-teal-600 text-white border-teal-600' : 'border-slate-300 text-slate-600 hover:bg-slate-50'">
                    {{ f.label }}
                  </button>
                }
              </div>
            </section>

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
                @if (suggestError()) { <p class="text-xs text-red-600 mt-1">{{ suggestError() }}</p> }
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

              <div class="grid sm:grid-cols-2 gap-4">
                <div>
                  <label class="lbl">Font</label>
                  <select [(ngModel)]="font" name="font" class="input" (ngModelChange)="schedulePreview()">
                    @for (f of o.fonts; track f) { <option [ngValue]="f">{{ f }}</option> }
                  </select>
                </div>
                <div>
                  <label class="lbl">Headline size</label>
                  <select [(ngModel)]="headlineScale" name="scale" class="input" (ngModelChange)="schedulePreview()">
                    <option value="small">Small</option>
                    <option value="medium">Medium</option>
                    <option value="large">Large</option>
                  </select>
                </div>
              </div>

              <div>
                <div class="flex items-center justify-between">
                  <label class="lbl">Colours</label>
                  <button type="button" (click)="resetColorsToBrand()" class="text-xs text-teal-700 hover:underline">Reset to brand</button>
                </div>
                <div class="flex gap-3 mt-1">
                  <label class="flex items-center gap-1.5 text-xs text-slate-600">
                    <input type="color" [(ngModel)]="primaryColor" name="primaryColor" class="w-8 h-8 rounded border border-slate-200 cursor-pointer" (ngModelChange)="schedulePreview()" /> Primary
                  </label>
                  <label class="flex items-center gap-1.5 text-xs text-slate-600">
                    <input type="color" [(ngModel)]="secondaryColor" name="secondaryColor" class="w-8 h-8 rounded border border-slate-200 cursor-pointer" (ngModelChange)="schedulePreview()" /> Secondary
                  </label>
                  <label class="flex items-center gap-1.5 text-xs text-slate-600">
                    <input type="color" [(ngModel)]="accentColor" name="accentColor" class="w-8 h-8 rounded border border-slate-200 cursor-pointer" (ngModelChange)="schedulePreview()" /> Accent
                  </label>
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

            @if (currentTemplate()?.usesPhoto) {
              <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
                <div>
                  <h2 class="text-sm font-semibold text-slate-800">AI background</h2>
                  <p class="text-xs text-slate-500">Add a real AI-generated scene to this template's photo medallion — the same engine as Product images. Your headline/price/CTA are still drawn crisply on top by us, not the model, so they always read correctly.</p>
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
                    @for (s of o.backgroundStyles; track s.key) { <option [ngValue]="s.key">{{ s.label }}</option> }
                  </select>
                  <button type="button" (click)="generateBackground()" [disabled]="generatingBg() || !canGenerateBackground()"
                          class="text-sm px-3 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 shrink-0 disabled:opacity-50">
                    {{ generatingBg() ? 'Generating…' : '✨ Generate (20 credits)' }}
                  </button>
                </div>
                @if (bgError()) { <p class="text-xs text-red-600">{{ bgError() }}</p> }
              </section>
            }

            @if (created() || editCreativeId()) {
              <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-2">
                <label class="lbl">Caption</label>
                <textarea [(ngModel)]="caption" name="caption" rows="3" class="input"></textarea>
              </section>
            }

            @if (!created() && !editCreativeId()) {
              <button type="button" (click)="create()" [disabled]="creating() || !canCreate()" class="btn-primary w-full disabled:opacity-60">
                {{ creating() ? 'Creating…' : 'Create this poster' }}
              </button>
              <p class="text-xs text-slate-400 text-center">Uses 1 AI credit for the post caption. The image itself is free.</p>
              @if (createError()) { <p class="text-xs text-red-600 text-center">{{ createError() }}</p> }
            } @else if (editCreativeId()) {
              <button type="button" (click)="save()" [disabled]="saving() || !canCreate()" class="btn-primary w-full disabled:opacity-60">
                {{ saving() ? 'Saving…' : 'Save changes' }}
              </button>
              <p class="text-xs text-slate-400 text-center">Free — only the AI buttons above spend credits.</p>
              @if (saved()) { <p class="text-xs text-green-700 text-center">✓ Saved</p> }
              @if (createError()) { <p class="text-xs text-red-600 text-center">{{ createError() }}</p> }
              <a routerLink="/admin/marketing/library" class="block text-center text-xs text-slate-400 hover:text-slate-600 underline">Back to Library</a>
            } @else {
              <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
                <p class="text-sm font-medium text-green-700">✓ Poster created</p>

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
                <a routerLink="/admin/marketing/library" class="block text-xs text-slate-400 hover:text-slate-600 underline">View in Library</a>
              </section>
            }
          </div>

          <!-- live preview -->
          <div class="lg:sticky lg:top-6 self-start">
            <div class="bg-white border border-slate-200 rounded-xl p-4 flex items-center justify-center overflow-hidden mx-auto"
                 [style.aspect-ratio]="previewAspect()" [style.max-width]="format === 'story' ? '380px' : '560px'">
              @if (previewUrl()) {
                <img [src]="previewUrl()" alt="Poster preview" class="max-w-full max-h-full rounded-lg" />
              } @else {
                <p class="text-sm text-slate-400">{{ previewing() ? 'Rendering…' : 'Preview will appear here' }}</p>
              }
            </div>
          </div>
        </div>
      } @else {
        <p class="text-sm text-slate-500">Loading…</p>
      }
    </div>
  `,
})
export class AdminMarketingPosterComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private previewTimer: ReturnType<typeof setTimeout> | null = null;

  readonly mode = signal<'browse' | 'editor'>('browse');
  readonly opts = signal<PosterEditorOptions | null>(null);
  readonly products = signal<NamedProduct[]>([]);
  readonly settings = signal<MarketingPlanSettings | null>(null);
  readonly brand = signal<MarketingBrand | null>(null);
  readonly backgroundUrl = signal<string | null>(null);
  readonly generatingBg = signal(false);
  readonly bgError = signal<string | null>(null);
  readonly suggestError = signal<string | null>(null);
  readonly createError = signal<string | null>(null);
  readonly thumbs = new Map<string, SafeUrl>();
  backgroundStyle = 'lifestyle';
  // SVG data URIs aren't on Angular's default image-src allowlist (only base64 raster formats are) —
  // bypassSecurityTrustUrl is safe here: this is our own server-rendered SVG, and a browser never
  // executes scripts embedded in an SVG loaded via <img src>, unlike inline/object/iframe embedding.
  readonly previewUrl = signal<SafeUrl | null>(null);
  readonly previewing = signal(false);
  readonly suggesting = signal(false);
  readonly creating = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly created = signal<{ itemId: number; mediaUrl: string } | null>(null);
  readonly editCreativeId = signal<number | null>(null);
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
  templateId = 'bold-medallion';
  font = 'Poppins';
  headlineScale = 'medium';
  format = 'square';
  primaryColor = '#111827';
  secondaryColor = '#6b7280';
  accentColor = '#2563eb';
  caption = '';

  ngOnInit(): void {
    this.api.videoOptions().subscribe((o) => this.products.set(o.products));
    this.api.getPlanSettings().subscribe((s) => this.settings.set(s));
    this.api.getBrand().subscribe((b) => {
      this.brand.set(b);
      if (!this.editCreativeId()) this.applyBrandColors(b);
    });
    this.api.posterOptions().subscribe((o) => {
      this.opts.set(o);
      if (o.fonts.length && !o.fonts.includes(this.font)) this.font = o.fonts[0];
      this.loadThumbnails(o.templates);
      this.route.queryParamMap.subscribe((params) => {
        const editId = params.get('edit');
        const template = params.get('template');
        if (editId) this.openForEdit(Number(editId));
        else if (template) this.startFromTemplate(template, false);
      });
    });
  }

  private applyBrandColors(b: MarketingBrand): void {
    this.primaryColor = b.primaryColor;
    this.secondaryColor = b.secondaryColor;
    this.accentColor = b.accentColor;
  }
  resetColorsToBrand(): void {
    const b = this.brand();
    if (b) this.applyBrandColors(b);
    this.schedulePreview();
  }

  categories(o: PosterEditorOptions): string[] {
    return Array.from(new Set(o.templates.map((t) => t.category)));
  }
  templatesIn(o: PosterEditorOptions, category: string): PosterTemplateInfo[] {
    return o.templates.filter((t) => t.category === category);
  }
  thumb(templateId: string): SafeUrl | null {
    return this.thumbs.get(templateId) ?? null;
  }
  private loadThumbnails(templates: PosterTemplateInfo[]): void {
    for (const t of templates) {
      this.api.previewPoster({
        kind: 'org', productId: null, headline: t.usesPhoto ? 'Big Festive Sale' : 'Flat 30% Off',
        price: 999, cta: 'Shop Now', includeLogo: false, includeName: true, templateId: t.id,
      }).subscribe((r) => this.thumbs.set(t.id, this.sanitizer.bypassSecurityTrustUrl(`data:image/svg+xml,${encodeURIComponent(r.svg)}`)));
    }
  }

  startFromTemplate(templateId: string, navigate = true): void {
    this.templateId = templateId;
    this.mode.set('editor');
    this.preview();
    if (navigate) this.router.navigate([], { queryParams: { template: templateId }, replaceUrl: true });
  }

  private openForEdit(creativeId: number): void {
    this.api.getPoster(creativeId).subscribe((detail) => {
      if (!detail.poster) { this.backToBrowse(); return; }
      const p = detail.poster;
      this.kind = p.kind;
      this.productId = p.productId;
      this.headline = p.headline;
      this.showPrice = p.price != null;
      this.price = p.price;
      this.cta = p.cta;
      this.includeLogo = p.includeLogo;
      this.includeName = p.includeName;
      this.templateId = p.templateId ?? 'bold-medallion';
      this.font = p.font ?? 'Poppins';
      this.headlineScale = p.headlineScale ?? 'medium';
      this.format = p.format ?? 'square';
      if (p.primaryColor) this.primaryColor = p.primaryColor;
      if (p.secondaryColor) this.secondaryColor = p.secondaryColor;
      if (p.accentColor) this.accentColor = p.accentColor;
      this.backgroundUrl.set(p.backgroundImageUrl ?? null);
      this.caption = detail.caption ?? '';
      this.editCreativeId.set(creativeId);
      this.mode.set('editor');
      this.preview();
    });
  }

  backToBrowse(): void {
    this.mode.set('browse');
    this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  currentTemplate() {
    return this.opts()?.templates.find((t) => t.id === this.templateId) ?? null;
  }
  currentFormat() {
    return this.opts()?.formats.find((f) => f.id === this.format) ?? this.opts()?.formats[0] ?? null;
  }
  previewAspect(): string {
    const f = this.currentFormat();
    return f ? `${f.width} / ${f.height}` : '1 / 1';
  }

  setTemplate(id: string): void {
    this.templateId = id;
    if (!this.currentTemplate()?.usesPhoto) this.backgroundUrl.set(null);
    this.schedulePreview();
  }
  setFormat(id: string): void { this.format = id; this.schedulePreview(); }
  setKind(k: 'org' | 'product'): void { this.kind = k; this.schedulePreview(); }

  onProductChange(): void {
    const p = this.products().find((x) => x.id === this.productId);
    if (p) { this.headline = p.name; this.showPrice = true; }
    this.schedulePreview();
  }

  suggestHeadline(): void {
    this.suggesting.set(true);
    this.suggestError.set(null);
    this.api.suggestPosterHeadline(this.kind === 'product' ? this.productId : null, this.headline || null).subscribe({
      next: (r) => { this.headline = r.headline; this.suggesting.set(false); this.preview(); },
      error: (err) => { this.suggesting.set(false); this.suggestError.set(this.describeError(err)); },
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
      templateId: this.templateId, font: this.font, headlineScale: this.headlineScale,
      format: this.format, primaryColor: this.primaryColor, secondaryColor: this.secondaryColor, accentColor: this.accentColor,
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
      error: (err) => { this.generatingBg.set(false); this.bgError.set(this.describeError(err)); },
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
    this.createError.set(null);
    this.api.createPoster(this.request()).subscribe({
      next: (r) => { this.created.set(r); this.caption = r.caption; this.creating.set(false); },
      error: (err) => { this.creating.set(false); this.createError.set(this.describeError(err)); },
    });
  }

  save(): void {
    const id = this.editCreativeId();
    if (!id) return;
    this.saving.set(true);
    this.createError.set(null);
    this.saved.set(false);
    this.api.updatePoster(id, this.request(), this.caption).subscribe({
      next: () => { this.saving.set(false); this.saved.set(true); this.preview(); },
      error: (err) => { this.saving.set(false); this.createError.set(this.describeError(err)); },
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
    this.editCreativeId.set(null);
    this.scheduled.set(false);
    this.saved.set(false);
    this.selected.clear();
    this.headline = '';
    this.productId = null;
    this.showPrice = false;
    this.caption = '';
    this.backgroundUrl.set(null);
    this.backToBrowse();
  }

  /** Surfaces the API's real error message (e.g. "you're out of AI credits") instead of a generic
   *  fallback — matches the app-wide 402/credit convention used across the admin. */
  private describeError(err: unknown): string {
    const e = err as { status?: number; error?: { message?: string } };
    if (e?.status === 402) return e.error?.message ?? 'Out of AI credits — top up on the AI credits page.';
    return e?.error?.message ?? 'Something went wrong. Please try again.';
  }
}
