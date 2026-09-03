import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { Observable, from, map, switchMap } from 'rxjs';
import { MediaService } from '../../../core/services/media.service';
import { PosterCanvasService } from '../../../core/services/poster-canvas.service';
import {
  ChannelPref, MarketingPlanSettings, NamedDescribedCode, NamedProduct, PosterDetail, PosterDocument,
  PosterEditorOptions, PosterStudioRequest, PosterTemplateInfo, MarketingStudioService,
} from '../../../core/services/marketing-studio.service';
import { PosterCanvasComponent } from './poster-canvas/poster-canvas.component';
import { PosterLayersPanelComponent } from './poster-canvas/poster-layers-panel.component';
import { PosterPropertiesPanelComponent } from './poster-canvas/poster-properties-panel.component';

/**
 * Poster Studio — a Canva-style Browse (category-grouped template gallery, one card per template x
 * format) that opens into a freeform canvas Editor. `?edit=<creativeId>` reopens a saved poster;
 * `?template=<id>&format=<id>` jumps straight into a fresh one. A LEGACY poster (made before the
 * canvas editor existed) opens view-only — it can still be viewed/duplicated from the Library, or
 * recreated as a fresh editable poster here, but not re-edited in place; see
 * PosterStudioService.GetAsync's SpecKind discriminator on the API side.
 */
@Component({
  selector: 'app-admin-marketing-poster',
  imports: [FormsModule, RouterLink, PosterCanvasComponent, PosterLayersPanelComponent, PosterPropertiesPanelComponent],
  providers: [PosterCanvasService],
  template: `
    <div class="max-w-6xl mx-auto p-6">
      @if (mode() === 'browse') {
        <div class="flex items-start justify-between gap-4 mb-6">
          <div>
            <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Poster Studio</h1>
            <p class="text-sm text-slate-500">Pick a template and a format to start. Nothing is created (or costs credits) until you save.</p>
          </div>
          <button type="button" (click)="autoFillDraft()" [disabled]="autoFilling()" class="text-sm px-3 py-2 rounded-lg border border-teal-300 text-teal-700 hover:bg-teal-50 disabled:opacity-50 shrink-0 whitespace-nowrap">
            {{ autoFilling() ? 'Filling…' : '✨ Auto-fill a draft' }}
          </button>
        </div>
        @if (autoFillError()) { <p class="text-xs text-red-600 mb-4">{{ autoFillError() }}</p> }

        @if (opts(); as o) {
          @for (cat of categories(o); track cat) {
            <div class="mb-7">
              <h2 class="text-sm font-semibold text-slate-700 mb-2.5">{{ cat }}</h2>
              <div class="grid sm:grid-cols-3 gap-4">
                @for (t of templatesIn(o, cat); track t.id) {
                  <div class="bg-white border border-slate-200 rounded-xl overflow-hidden hover:border-teal-400 hover:shadow-sm transition-all">
                    <button type="button" (click)="startFromTemplate(t.id, 'square')" class="block w-full text-left">
                      <div class="aspect-square bg-slate-100 flex items-center justify-center">
                        @if (thumb(t.id); as svg) {
                          <img [src]="svg" alt="" class="w-full h-full object-cover" />
                        } @else {
                          <span class="text-xs text-slate-400">Loading…</span>
                        }
                      </div>
                    </button>
                    <div class="p-3">
                      <div class="text-sm font-semibold text-slate-800">{{ t.name }}</div>
                      <div class="text-xs text-slate-500 mt-0.5 line-clamp-2">{{ t.description }}</div>
                      @if (!t.usesPhoto) { <div class="text-xs text-teal-700 mt-1 font-medium">No photo needed</div> }
                      <div class="flex gap-2 mt-2">
                        @for (f of o.formats; track f.id) {
                          <button type="button" (click)="startFromTemplate(t.id, f.id)" class="text-xs px-2 py-1 rounded-full border border-slate-200 text-slate-600 hover:border-teal-400 hover:text-teal-700">
                            {{ f.label.split('·')[0].trim() }}
                          </button>
                        }
                      </div>
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        } @else {
          <p class="text-sm text-slate-500">Loading…</p>
        }
      } @else {
        <div class="flex items-center gap-2 mb-4">
          <button type="button" (click)="backToBrowse()" class="text-sm text-slate-500 hover:text-slate-700">← Templates</button>
        </div>

        @if (specKind() === 'legacy') {
          <section class="max-w-md mx-auto bg-white border border-slate-200 rounded-xl p-5 space-y-3 text-center">
            <p class="text-sm font-medium text-slate-700">This poster predates the canvas editor</p>
            @if (legacyMediaUrl(); as url) { <img [src]="url" alt="Poster" class="max-w-full rounded-lg border border-slate-200 mx-auto" /> }
            <p class="text-xs text-slate-500">It's still viewable and can be duplicated from the Library, but can't be edited in place here.</p>
            <button type="button" (click)="recreateAsEditable()" class="btn-primary w-full">Recreate as an editable poster</button>
            <p class="text-xs text-slate-400">Starts a new, fully editable poster from this image — the original stays untouched.</p>
            <a routerLink="/admin/marketing/library" class="inline-block text-sm text-teal-700 hover:underline">Back to Library</a>
          </section>
        } @else if (activeDoc(); as doc) {
          <div class="grid lg:grid-cols-[1fr_320px] gap-6">
            <app-poster-canvas #canvas [document]="doc" class="min-w-0" />

            <div class="space-y-4">
              <app-poster-layers-panel />
              <app-poster-properties-panel />

              <section class="bg-white border border-slate-200 rounded-xl p-4 space-y-3">
                <div class="flex items-center justify-between">
                  <label class="lbl">Headline</label>
                  <button type="button" (click)="suggestHeadline()" [disabled]="suggestingHeadline()" class="text-xs text-teal-700 hover:underline disabled:opacity-50">
                    {{ suggestingHeadline() ? '…' : '✨ Suggest' }}
                  </button>
                </div>
                @if (suggestError()) { <p class="text-xs text-red-600">{{ suggestError() }}</p> }

                @if (usesPhoto()) {
                  <div class="pt-2 border-t border-slate-100 space-y-2">
                    <label class="lbl">AI background</label>
                    <div class="flex gap-2">
                      <select [(ngModel)]="backgroundStyle" name="bgStyle" class="input flex-1">
                        @for (s of backgroundStyles(); track s.key) { <option [ngValue]="s.key">{{ s.label }}</option> }
                      </select>
                      <button type="button" (click)="generateBackground()" [disabled]="generatingBg()"
                              class="text-sm px-3 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50 shrink-0 disabled:opacity-50">
                        {{ generatingBg() ? 'Generating…' : '✨ (20 credits)' }}
                      </button>
                    </div>
                    @if (bgError()) { <p class="text-xs text-red-600">{{ bgError() }}</p> }
                  </div>
                }
              </section>

              <section class="bg-white border border-slate-200 rounded-xl p-4 space-y-2">
                <label class="lbl">Caption</label>
                <textarea [(ngModel)]="caption" name="caption" rows="3" class="input" placeholder="Leave blank for an AI-written caption"></textarea>
              </section>

              @if (!editCreativeId()) {
                <button type="button" (click)="create(canvas)" [disabled]="creating()" class="btn-primary w-full disabled:opacity-60">
                  {{ creating() ? 'Creating…' : 'Create this poster' }}
                </button>
                <p class="text-xs text-slate-400 text-center">Uses 1 AI credit only if the caption is left blank.</p>
                @if (createError()) { <p class="text-xs text-red-600 text-center">{{ createError() }}</p> }
              } @else {
                <button type="button" (click)="save(canvas)" [disabled]="saving()" class="btn-primary w-full disabled:opacity-60">
                  {{ saving() ? 'Saving…' : 'Save changes' }}
                </button>
                <p class="text-xs text-slate-400 text-center">Free — layout/text edits never spend credits.</p>
                @if (saved()) { <p class="text-xs text-green-700 text-center">✓ Saved</p> }
                @if (createError()) { <p class="text-xs text-red-600 text-center">{{ createError() }}</p> }
                <a routerLink="/admin/marketing/library" class="block text-center text-xs text-slate-400 hover:text-slate-600 underline">Back to Library</a>
              }

              @if (created(); as c) {
                <section class="bg-white border border-slate-200 rounded-xl p-4 space-y-3">
                  <p class="text-sm font-medium text-green-700">✓ Poster created</p>
                  @if (settings(); as s) {
                    @if (posterChannels(s).length === 0) {
                      <p class="text-xs text-slate-400">Connect a channel that allows posters to schedule this. <a routerLink="/admin/marketing/connections" class="underline">Connections</a></p>
                    } @else if (!scheduled()) {
                      <div class="flex flex-wrap items-center gap-2">
                        @for (ch of posterChannels(s); track ch.platform) {
                          <label class="flex items-center gap-1 text-xs text-slate-600 border border-slate-200 rounded-full px-2 py-0.5 cursor-pointer"
                                 [class.bg-teal-50]="selected.has(ch.platform)" [class.border-teal-300]="selected.has(ch.platform)">
                            <input type="checkbox" class="sr-only" [checked]="selected.has(ch.platform)" (change)="toggleChannel(ch.platform)" />
                            {{ ch.displayName }}
                          </label>
                        }
                        <button type="button" (click)="scheduleNow(c.itemId)" [disabled]="scheduling() || selected.size === 0" class="text-xs font-medium text-teal-700 hover:underline disabled:opacity-50">
                          {{ scheduling() ? 'Scheduling…' : 'Schedule' }}
                        </button>
                      </div>
                    } @else {
                      <p class="text-xs text-green-700">Scheduled — waiting for approval on the <a routerLink="/admin/marketing/scheduler" class="underline">Scheduler</a>.</p>
                    }
                  }
                  <a routerLink="/admin/marketing/library" class="block text-xs text-slate-400 hover:text-slate-600 underline">View in Library</a>
                </section>
              }
            </div>
          </div>
        } @else {
          <p class="text-sm text-slate-500">Loading…</p>
        }
      }
    </div>
  `,
})
export class AdminMarketingPosterComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);
  private readonly mediaApi = inject(MediaService);
  private readonly canvasSvc = inject(PosterCanvasService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly mode = signal<'browse' | 'editor'>('browse');
  readonly opts = signal<PosterEditorOptions | null>(null);
  readonly products = signal<NamedProduct[]>([]);
  readonly settings = signal<MarketingPlanSettings | null>(null);
  readonly thumbs = new Map<string, SafeUrl>();

  readonly activeDoc = signal<PosterDocument | null>(null);
  readonly specKind = signal<PosterDetail['specKind'] | null>(null);
  readonly legacyMediaUrl = signal<string | null>(null);
  readonly editCreativeId = signal<number | null>(null);

  readonly creating = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly createError = signal<string | null>(null);
  readonly created = signal<{ itemId: number } | null>(null);
  readonly scheduled = signal(false);
  readonly scheduling = signal(false);
  readonly selected = new Set<string>();

  readonly autoFilling = signal(false);
  readonly autoFillError = signal<string | null>(null);
  readonly suggestingHeadline = signal(false);
  readonly suggestError = signal<string | null>(null);
  readonly generatingBg = signal(false);
  readonly bgError = signal<string | null>(null);
  readonly backgroundStyles = signal<NamedDescribedCode[]>([]);
  backgroundStyle = 'lifestyle';

  caption = '';

  ngOnInit(): void {
    this.api.videoOptions().subscribe((o) => this.products.set(o.products));
    this.api.getPlanSettings().subscribe((s) => this.settings.set(s));
    this.api.posterBackgroundStyles().subscribe((s) => this.backgroundStyles.set(s));
    this.api.posterOptions().subscribe((o) => {
      this.opts.set(o);
      this.loadThumbnails(o.templates);
      this.route.queryParamMap.subscribe((params) => {
        const editId = params.get('edit');
        const template = params.get('template');
        const format = params.get('format') ?? 'square';
        if (editId) this.openForEdit(Number(editId));
        else if (template) this.startFromTemplate(template, format, false);
      });
    });
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
    // The old flat-SVG preview endpoint still exists (untouched by the canvas rewrite) and is a cheap,
    // deterministic way to render a representative thumbnail without booting an off-screen canvas.
    for (const t of templates) {
      this.api.previewPoster({
        kind: 'org', productId: null, headline: t.usesPhoto ? 'Big Festive Sale' : 'Flat 30% Off',
        price: 999, cta: 'Shop Now', includeLogo: false, includeName: true, templateId: t.id,
      }).subscribe((r) => this.thumbs.set(t.id, this.sanitizer.bypassSecurityTrustUrl(`data:image/svg+xml,${encodeURIComponent(r.svg)}`)));
    }
  }

  /** True once a document with a photo/background-role layer is loaded — gates the AI background
   *  section, which only makes sense for templates that have somewhere to put a generated scene. */
  usesPhoto(): boolean {
    return this.activeDoc()?.layers.some((l) => l.role === 'photo' || l.role === 'background') ?? false;
  }

  startFromTemplate(templateId: string, format: string, navigate = true, overrides?: { headline?: string; showPrice?: boolean }): void {
    this.api.templateDocument(templateId, format).subscribe((doc) => {
      this.resetEditorState();
      let layers = doc.layers;
      if (overrides?.headline) layers = layers.map((l) => (l.role === 'headline' ? { ...l, text: overrides.headline! } : l));
      if (overrides?.showPrice === false) layers = layers.filter((l) => l.role !== 'price');
      this.activeDoc.set({ ...doc, layers });
      this.specKind.set('layers-v1');
      this.mode.set('editor');
    });
    if (navigate) this.router.navigate([], { queryParams: { template: templateId, format }, replaceUrl: true });
  }

  /** Picks a template + headline instead of a blank Browse page. Rule-based template pick + the
   *  existing headline suggester (server side, unchanged) — never triggers the paid AI background
   *  generation on its own, that stays an explicit opt-in click once in the Editor. */
  autoFillDraft(): void {
    this.autoFilling.set(true);
    this.autoFillError.set(null);
    this.api.autoFillPosterDraft('org', null).subscribe({
      next: (draft) => {
        this.autoFilling.set(false);
        this.startFromTemplate(draft.templateId, 'square', true, { headline: draft.headline, showPrice: draft.showPrice });
      },
      error: (err) => { this.autoFilling.set(false); this.autoFillError.set(this.describeError(err)); },
    });
  }

  /** Writes an AI-suggested headline into the document's headline-role text layer (or the first text
   *  layer, if a template has none tagged) — the layer-aware equivalent of the old flat-field editor's
   *  "Suggest" button next to a single headline input. */
  suggestHeadline(): void {
    const doc = this.activeDoc();
    if (!doc) return;
    this.suggestingHeadline.set(true);
    this.suggestError.set(null);
    const productId = doc.kind === 'product' ? doc.productId ?? null : null;
    this.api.suggestPosterHeadline(productId, null).subscribe({
      next: (r) => {
        this.suggestingHeadline.set(false);
        const layers = this.canvasSvc.layers();
        const target = layers.find((l) => l.role === 'headline') ?? layers.find((l) => l.type === 'text');
        if (target) this.canvasSvc.setLayerProp(target.id, { text: r.headline });
      },
      error: (err) => { this.suggestingHeadline.set(false); this.suggestError.set(this.describeError(err)); },
    });
  }

  /** Generates a real AI scene (the same pipeline "Product images" uses) and drops it onto the
   *  document's photo/background-role layer — credit-metered, explicit opt-in. Reuses the legacy
   *  PosterStudioRequest shape purely as this endpoint's wire format; only Kind/ProductId/Headline are
   *  actually read server-side for this call, the rest are unused placeholders. */
  generateBackground(): void {
    const doc = this.activeDoc();
    if (!doc) return;
    const headline = this.canvasSvc.layers().find((l) => l.role === 'headline')?.text ?? '';
    const req: PosterStudioRequest = {
      kind: doc.kind === 'product' ? 'product' : 'org', productId: doc.productId ?? null,
      headline, price: null, cta: 'Shop Now', includeLogo: true, includeName: true,
    };
    this.generatingBg.set(true);
    this.bgError.set(null);
    this.api.generatePosterBackground(req, this.backgroundStyle).subscribe({
      next: (r) => {
        this.generatingBg.set(false);
        const target = this.canvasSvc.layers().find((l) => l.role === 'photo' || l.role === 'background');
        if (target) void this.canvasSvc.setLayerImage(target.id, r.url);
        else this.canvasSvc.addImageLayer(r.url, 'photo');
      },
      error: (err) => { this.generatingBg.set(false); this.bgError.set(this.describeError(err)); },
    });
  }

  /** A legacy poster's flat image, wrapped as a single full-bleed layer in a brand-new editable
   *  document — the original creative/row is untouched; this becomes a genuinely new poster on Create,
   *  not an in-place migration of the old one. */
  recreateAsEditable(): void {
    const url = this.legacyMediaUrl();
    if (!url) return;
    this.resetEditorState();
    this.activeDoc.set({
      specVersion: 'layers-v1',
      format: { width: 1080, height: 1080 },
      background: { type: 'color', color: '#ffffff' },
      layers: [{
        id: crypto.randomUUID(), type: 'image', x: 0, y: 0, width: 1080, height: 1080,
        rotation: 0, opacity: 1, zIndex: 0, role: 'background', imageUrl: url, fit: 'cover',
      }],
    });
    this.specKind.set('layers-v1');
  }

  private openForEdit(creativeId: number): void {
    this.api.getPoster(creativeId).subscribe((detail) => {
      this.resetEditorState();
      this.specKind.set(detail.specKind);
      this.caption = detail.caption ?? '';
      if (detail.specKind === 'layers-v1' && detail.document) {
        this.activeDoc.set(detail.document);
        this.editCreativeId.set(creativeId);
      } else if (detail.specKind === 'legacy') {
        this.legacyMediaUrl.set(detail.mediaUrl);
      } else {
        this.backToBrowse();
        return;
      }
      this.mode.set('editor');
    });
  }

  private resetEditorState(): void {
    this.activeDoc.set(null);
    this.specKind.set(null);
    this.legacyMediaUrl.set(null);
    this.editCreativeId.set(null);
    this.created.set(null);
    this.scheduled.set(false);
    this.saved.set(false);
    this.createError.set(null);
    this.suggestError.set(null);
    this.bgError.set(null);
    this.caption = '';
    this.selected.clear();
  }

  backToBrowse(): void {
    this.mode.set('browse');
    this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  create(canvas: PosterCanvasComponent): void {
    this.creating.set(true);
    this.createError.set(null);
    this.exportAndUpload(canvas).subscribe({
      next: ({ doc, mediaFileId }) => {
        this.api.createPosterDocument(doc, mediaFileId, this.caption || null).subscribe({
          next: (r) => { this.creating.set(false); this.created.set({ itemId: r.itemId }); this.caption = r.caption; },
          error: (err) => { this.creating.set(false); this.createError.set(this.describeError(err)); },
        });
      },
      error: (err) => { this.creating.set(false); this.createError.set(this.describeError(err)); },
    });
  }

  save(canvas: PosterCanvasComponent): void {
    const id = this.editCreativeId();
    if (!id) return;
    this.saving.set(true);
    this.createError.set(null);
    this.saved.set(false);
    this.exportAndUpload(canvas).subscribe({
      next: ({ doc, mediaFileId }) => {
        this.api.updatePosterDocument(id, doc, mediaFileId, this.caption).subscribe({
          next: () => { this.saving.set(false); this.saved.set(true); },
          error: (err) => { this.saving.set(false); this.createError.set(this.describeError(err)); },
        });
      },
      error: (err) => { this.saving.set(false); this.createError.set(this.describeError(err)); },
    });
  }

  /** Exports the canvas to PNG, uploads it through the existing admin media endpoint, and hands back
   *  both the reassembled document and the new media file's id for the create/update call. */
  private exportAndUpload(canvas: PosterCanvasComponent): Observable<{ doc: PosterDocument; mediaFileId: number }> {
    const doc = canvas.toDocument();
    return from(canvas.exportPng()).pipe(
      switchMap((blob) => this.mediaApi.upload(new File([blob], 'poster.png', { type: 'image/png' }))),
      map((media) => ({ doc, mediaFileId: media.mediaFileId })),
    );
  }

  posterChannels(s: MarketingPlanSettings): ChannelPref[] {
    return s.channels.filter((c) => c.enabled && c.allowPoster);
  }

  toggleChannel(platform: string): void {
    if (this.selected.has(platform)) this.selected.delete(platform); else this.selected.add(platform);
  }

  scheduleNow(itemId: number): void {
    this.scheduling.set(true);
    this.api.scheduleItem(itemId, Array.from(this.selected)).subscribe({
      next: () => { this.scheduling.set(false); this.scheduled.set(true); },
      error: () => this.scheduling.set(false),
    });
  }

  /** Surfaces the API's real error message (e.g. "you're out of AI credits") instead of a generic
   *  fallback — matches the app-wide 402/credit convention used across the admin. */
  private describeError(err: unknown): string {
    const e = err as { status?: number; error?: { message?: string } };
    if (e?.status === 402) return e.error?.message ?? 'Out of AI credits — top up on the AI credits page.';
    return e?.error?.message ?? 'Something went wrong. Please try again.';
  }
}
