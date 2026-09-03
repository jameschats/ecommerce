import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { Observable, from, map, switchMap } from 'rxjs';
import { MediaService } from '../../../core/services/media.service';
import {
  ChannelPref, MarketingPlanSettings, NamedProduct, PosterDetail, PosterDocument, PosterEditorOptions,
  PosterTemplateInfo, MarketingStudioService,
} from '../../../core/services/marketing-studio.service';
import { PosterCanvasComponent } from './poster-canvas/poster-canvas.component';

/**
 * Poster Studio — a Canva-style Browse (category-grouped template gallery, one card per template x
 * format) that opens into a freeform canvas Editor. `?edit=<creativeId>` reopens a saved poster;
 * `?template=<id>&format=<id>` jumps straight into a fresh one. A LEGACY poster (made before the
 * canvas editor existed) opens view-only — it can still be viewed/duplicated from the Library, just
 * not re-edited here; see PosterStudioService.GetAsync's SpecKind discriminator on the API side.
 */
@Component({
  selector: 'app-admin-marketing-poster',
  imports: [FormsModule, RouterLink, PosterCanvasComponent],
  template: `
    <div class="max-w-6xl mx-auto p-6">
      @if (mode() === 'browse') {
        <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Poster Studio</h1>
        <p class="text-sm text-slate-500 mb-6">Pick a template and a format to start. Nothing is created (or costs credits) until you save.</p>

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
            <p class="text-xs text-slate-500">It's still viewable and can be duplicated from the Library, but can't be re-edited here.</p>
            <a routerLink="/admin/marketing/library" class="inline-block text-sm text-teal-700 hover:underline">Back to Library</a>
          </section>
        } @else if (activeDoc(); as doc) {
          <div class="grid lg:grid-cols-[1fr,320px] gap-6">
            <app-poster-canvas #canvas [document]="doc" />

            <div class="space-y-4">
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

  caption = '';

  ngOnInit(): void {
    this.api.videoOptions().subscribe((o) => this.products.set(o.products));
    this.api.getPlanSettings().subscribe((s) => this.settings.set(s));
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

  startFromTemplate(templateId: string, format: string, navigate = true): void {
    this.api.templateDocument(templateId, format).subscribe((doc) => {
      this.resetEditorState();
      this.activeDoc.set(doc);
      this.specKind.set('layers-v1');
      this.mode.set('editor');
    });
    if (navigate) this.router.navigate([], { queryParams: { template: templateId, format }, replaceUrl: true });
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
