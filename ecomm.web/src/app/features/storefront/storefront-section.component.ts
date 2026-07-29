import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, HostBinding, OnDestroy, OnInit, PLATFORM_ID, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CatalogService } from '../../core/services/catalog.service';
import { BuilderSection } from '../../core/services/cms.service';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { ThemeService } from '../../core/services/theme.service';

/**
 * Renders one storefront section from its type + settings/blocks JSON.
 * The single source of truth for how a section looks (matches the Section-Type
 * schema on the backend). RichText is bound via [innerHTML]; it was sanitized
 * server-side, and Angular sanitizes again here.
 */
@Component({
  selector: 'app-storefront-section',
  imports: [RouterLink],
  template: `
    @switch (section().sectionType) {
      @case ('Hero') {
        @switch (s().style) {
          @case ('banner') {
            @if (blocks()[0]; as b) {
              <section class="relative min-h-[360px] sm:min-h-[440px] flex items-center bg-slate-900"
                       [style.background-image]="b.image ? 'url(' + b.image + ')' : null" style="background-size:cover;background-position:center">
                <div class="absolute inset-0" style="background:linear-gradient(90deg, rgba(0,0,0,0.72), rgba(0,0,0,0.15))"></div>
                <div class="relative page-container text-white">
                  <div class="max-w-xl">
                    @if (b.heading) { <h2 class="text-4xl sm:text-5xl font-extrabold leading-tight">{{ b.heading }}</h2> }
                    @if (b.subheading) { <p class="mt-3 text-white/85 text-lg">{{ b.subheading }}</p> }
                    @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-6 px-6 py-3 rounded-lg bg-primary text-white font-medium">{{ b.buttonText }}</a> }
                  </div>
                </div>
              </section>
            }
          }
          @case ('split') {
            @if (blocks()[0]; as b) {
              <section class="page-container py-8">
                <div class="grid md:grid-cols-2 items-stretch rounded-2xl overflow-hidden" style="background:var(--color-secondary,#0f172a)">
                  <div class="p-10 flex flex-col justify-center text-white">
                    @if (b.heading) { <h2 class="text-3xl sm:text-4xl font-bold">{{ b.heading }}</h2> }
                    @if (b.subheading) { <p class="mt-3 text-white/80">{{ b.subheading }}</p> }
                    @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-5 px-5 py-2.5 rounded-lg bg-white text-slate-900 font-medium w-fit">{{ b.buttonText }}</a> }
                  </div>
                  @if (b.image) { <img [src]="b.image" alt="" class="w-full h-full object-cover min-h-[280px]" /> }
                </div>
              </section>
            }
          }
          @case ('panels') {
            <!-- Three-panel hero: colour panel with copy flanked by two images (playful/kids look). -->
            @if (blocks()[0]; as b) {
              <section class="page-container py-6">
                <div class="grid md:grid-cols-[1fr_1.7fr_1fr] gap-4 items-stretch">
                  @if (blocks()[1]; as l) { <div class="hidden md:block overflow-hidden sf-card"><img [src]="l.image" alt="" class="w-full h-full object-cover" /></div> }
                  <div class="p-8 sm:p-10 flex flex-col justify-center min-h-[320px] overflow-hidden" style="border-radius: var(--radius-card, 0.75rem)"
                       [style.background-color]="theme.resolveBg(s()['colorScheme'], s().backgroundColor, 'var(--color-primary)')"
                       [style.color]="theme.resolveText(s()['colorScheme'], '#ffffff')">
                    @if (b.heading) { <h2 class="text-3xl sm:text-4xl font-extrabold leading-tight">{{ b.heading }}</h2> }
                    @if (b.subheading) { <p class="mt-3 text-white/85">{{ b.subheading }}</p> }
                    @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-5 px-5 py-2.5 rounded-lg bg-white text-slate-900 font-medium w-fit">{{ b.buttonText }}</a> }
                  </div>
                  @if (blocks()[2]; as r) { <div class="hidden md:block overflow-hidden sf-card"><img [src]="r.image" alt="" class="w-full h-full object-cover" /></div> }
                </div>
              </section>
            }
          }
          @default {
            <section class="relative">
              @for (b of blocks(); track $index) {
                <div class="relative min-h-[320px] flex items-center justify-center text-center bg-slate-900 text-white"
                     [style.background-image]="b.image ? 'url(' + b.image + ')' : null" style="background-size:cover;background-position:center">
                  <div class="bg-black/30 absolute inset-0"></div>
                  <div class="relative p-8 max-w-2xl">
                    @if (b.heading) { <h2 class="text-3xl sm:text-4xl font-bold">{{ b.heading }}</h2> }
                    @if (b.subheading) { <p class="mt-2 text-slate-200">{{ b.subheading }}</p> }
                    @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-4 px-5 py-2 rounded-lg bg-primary text-white font-medium">{{ b.buttonText }}</a> }
                  </div>
                </div>
              }
              @if (!blocks().length) { <div class="min-h-[200px] grid place-items-center text-slate-300 bg-slate-100">Add slides to this hero</div> }
            </section>
          }
        }
      }
      @case ('Multicolumn') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-6 text-center">{{ s().heading }}</h2> }
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
            @for (b of blocks(); track $index) {
              <div class="text-center p-5 sf-card" [attr.data-block-index]="$index">
                @if (b.icon) { <div class="text-3xl">{{ b.icon }}</div> }
                @if (b.heading) { <h3 class="font-semibold text-slate-900 mt-2">{{ b.heading }}</h3> }
                @if (b.text) { <p class="text-sm text-slate-500 mt-1">{{ b.text }}</p> }
              </div>
            }
          </div>
        </section>
      }
      @case ('TileGrid') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5">{{ s().heading }}</h2> }
          <div class="grid gap-4" [class]="tileCols()">
            @for (b of blocks(); track $index) {
              <a [href]="b.link || '/products'" class="group block overflow-hidden sf-card" [attr.data-block-index]="$index">
                <div class="aspect-[4/5] bg-slate-100 overflow-hidden">
                  @if (b.image) { <img [src]="b.image" [alt]="b.label || ''" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
                </div>
                @if (b.label || b.sublabel) {
                  <div class="p-3 text-center">
                    @if (b.label) { <div class="font-semibold text-slate-800">{{ b.label }}</div> }
                    @if (b.sublabel) { <div class="text-xs text-slate-500 mt-0.5">{{ b.sublabel }}</div> }
                  </div>
                }
              </a>
            }
          </div>
        </section>
      }
      @case ('PromoTiles') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5">{{ s().heading }}</h2> }
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-4">
            @for (b of blocks(); track $index) {
              <a [href]="b.link || '/products'" class="relative block overflow-hidden min-h-[170px] sf-card" [attr.data-block-index]="$index"
                 [style.background-color]="theme.resolveBg(b.colorScheme, b.backgroundColor, 'var(--color-secondary, #0f172a)')">
                @if (b.image) { <img [src]="b.image" alt="" class="absolute inset-0 w-full h-full object-cover" loading="lazy" /> }
                <div class="absolute inset-0 bg-gradient-to-t from-black/60 via-black/10 to-transparent"></div>
                <div class="relative p-4 flex flex-col justify-end h-full min-h-[170px]" [style.color]="theme.resolveText(b.colorScheme, '#ffffff')">
                  @if (b.badge) { <span class="text-[11px] font-bold uppercase tracking-wide bg-white/90 text-slate-900 rounded px-1.5 py-0.5 w-fit mb-1.5">{{ b.badge }}</span> }
                  @if (b.heading) { <div class="font-bold leading-snug">{{ b.heading }}</div> }
                  @if (b.text) { <div class="text-xs text-white/80 mt-0.5">{{ b.text }}</div> }
                </div>
              </a>
            }
          </div>
        </section>
      }
      @case ('Marquee') {
        <div class="overflow-hidden py-2.5 text-sm font-medium"
             [style.background-color]="theme.resolveBg(s()['colorScheme'], s().backgroundColor, '#111827')"
             [style.color]="theme.resolveText(s()['colorScheme'], '#ffffff')">
          <div class="marquee-x flex whitespace-nowrap w-max">
            @for (i of ph; track i) {
              <span class="mx-6">{{ s().text || 'Free shipping over ₹499' }}</span><span class="opacity-50">✦</span>
              <span class="mx-6">{{ s().text || 'Free shipping over ₹499' }}</span><span class="opacity-50">✦</span>
            }
          </div>
        </div>
      }
      @case ('CountdownBar') {
        <div class="py-3 px-4 flex flex-wrap items-center justify-center gap-3 text-sm font-medium text-center"
             [style.background-color]="theme.resolveBg(s()['colorScheme'], s().backgroundColor, '#111827')"
             [style.color]="theme.resolveText(s()['colorScheme'], '#ffffff')">
          @if (remaining(); as r) {
            @if (s().heading) { <span>{{ s().heading }}</span> }
            <span class="font-mono font-bold tabular-nums">{{ r.days }}d {{ r.hours }}h {{ r.mins }}m {{ r.secs }}s</span>
          } @else {
            <span>{{ s().expiredText || 'This offer has ended' }}</span>
          }
          @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="ml-2 px-3 py-1 rounded-lg bg-white/15 hover:bg-white/25 font-medium">{{ s().buttonText }}</a> }
        </div>
      }
      @case ('RichText') {
        <div class="max-w-3xl mx-auto px-4 py-8 prose" [style.text-align]="s().align || 'left'" [innerHTML]="s().content"></div>
      }
      @case ('ImageWithText') {
        <section class="max-w-5xl mx-auto px-4 py-10 grid sm:grid-cols-2 gap-8 items-center" [class.sm:flex-row-reverse]="s().imageSide === 'right'">
          @if (s().image) { <img [src]="s().image" alt="" class="rounded-xl w-full object-cover" [class.sm:order-2]="s().imageSide === 'right'" /> }
          <div>
            @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900">{{ s().heading }}</h2> }
            @if (s().body) { <p class="mt-2 text-slate-600">{{ s().body }}</p> }
            @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="inline-block mt-4 px-5 py-2 rounded-lg bg-primary text-white font-medium">{{ s().buttonText }}</a> }
          </div>
        </section>
      }
      @case ('Testimonials') {
        <section class="max-w-5xl mx-auto px-4 py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 text-center mb-6">{{ s().heading }}</h2> }
          <div class="grid sm:grid-cols-3 gap-4">
            @for (b of blocks(); track $index) {
              <div class="bg-white border border-slate-200 rounded-xl p-5" [attr.data-block-index]="$index">
                <div class="text-amber-400">{{ stars(b.rating) }}</div>
                <p class="text-slate-600 mt-2">"{{ b.quote }}"</p>
                <div class="text-sm font-medium text-slate-800 mt-3">— {{ b.author }}</div>
              </div>
            }
          </div>
        </section>
      }
      @case ('CtaNewsletter') {
        <section class="py-12 text-center"
                 [style.background-color]="theme.resolveBg(s()['colorScheme'], s().backgroundColor, '#111827')"
                 [style.color]="theme.resolveText(s()['colorScheme'], '#ffffff')">
          @if (s().heading) { <h2 class="text-2xl font-bold">{{ s().heading }}</h2> }
          @if (s().subtext) { <p class="mt-1 text-white/80">{{ s().subtext }}</p> }
          @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="inline-block mt-4 px-6 py-2 rounded-lg bg-white text-slate-900 font-medium">{{ s().buttonText }}</a> }
        </section>
      }
      @case ('Categories') {
        <section class="max-w-6xl mx-auto px-4 py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5">{{ s().heading }}</h2> }
          @if (categories().length) {
            @if (s().style === 'cards') {
              <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
                @for (c of categories(); track c.categoryId) {
                  <a [routerLink]="['/category', c.slug]" class="group block overflow-hidden sf-card">
                    <div class="aspect-[4/3] bg-slate-100 overflow-hidden">
                      @if (c.imageUrl) { <img [src]="c.imageUrl" alt="" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
                    </div>
                    <div class="p-3 text-center text-sm font-semibold text-slate-800">{{ c.name }}</div>
                  </a>
                }
              </div>
            } @else {
              <div class="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3">
                @for (c of categories(); track c.categoryId) {
                  <a [routerLink]="['/category', c.slug]" class="block p-4 text-center hover:border-primary transition sf-card">
                    @if (c.imageUrl) { <img [src]="c.imageUrl" alt="" class="w-12 h-12 mx-auto object-contain mb-2" /> }
                    <div class="text-sm font-medium text-slate-700">{{ c.name }}</div>
                  </a>
                }
              </div>
            }
          } @else {
            <!-- No categories yet — placeholder tiles so the layout reads (real ones replace these). -->
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              @for (i of ph; track i) {
                <div class="overflow-hidden sf-card">
                  <div class="aspect-[4/3] bg-slate-100"></div>
                  <div class="p-3 flex justify-center"><div class="h-3 w-20 bg-slate-100 rounded"></div></div>
                </div>
              }
            </div>
          }
        </section>
      }
      @default {
        <!-- FeaturedProducts / ProductGrid / any product rail -->
        <section class="max-w-6xl mx-auto px-4 py-10">
          @if (s().heading || section().title) { <h2 class="text-2xl font-bold text-slate-900 mb-5">{{ s().heading || section().title }}</h2> }
          @if (!products().length) {
            <!-- No products yet — skeleton cards so the layout reads (real ones replace these). -->
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              @for (i of ph; track i) {
                <div class="overflow-hidden sf-card">
                  <div class="aspect-square bg-slate-100"></div>
                  <div class="p-3 space-y-2"><div class="h-3 w-3/4 bg-slate-100 rounded"></div><div class="h-3 w-1/3 bg-slate-100 rounded"></div></div>
                </div>
              }
            </div>
          } @else if (s().layout === 'carousel') {
            <div class="flex gap-4 overflow-x-auto no-scrollbar snap-x pb-2">
              @for (p of products(); track p.productId) {
                <a [routerLink]="['/product', p.slug]" class="snap-start shrink-0 w-44 sm:w-52 block overflow-hidden sf-card">
                  <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                    @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                  </div>
                  <div class="p-3"><div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div><div class="text-slate-900 font-bold mt-1">₹{{ p.price }}</div></div>
                </a>
              }
            </div>
          } @else {
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              @for (p of products(); track p.productId) {
                <a [routerLink]="['/product', p.slug]" class="block overflow-hidden sf-card">
                  <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                    @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                  </div>
                  <div class="p-3"><div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div><div class="text-slate-900 font-bold mt-1">₹{{ p.price }}</div></div>
                </a>
              }
            </div>
          }
        </section>
      }
    }
  `,
  styles: [`:host.theme-editor-selected { outline: 2px solid #2563eb; outline-offset: -2px; }`],
})
export class StorefrontSectionComponent implements OnInit, OnDestroy {
  private readonly catalog = inject(CatalogService);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly theme = inject(ThemeService);
  readonly section = input.required<BuilderSection>();

  readonly products = signal<ProductListItem[]>([]);
  readonly categories = signal<Category[]>([]);
  /** Placeholder tiles shown when a section has no catalog data yet (fresh store / preview). */
  readonly ph = [0, 1, 2, 3];

  readonly s = computed<any>(() => this.parse<any>(this.section().settings, {}));
  readonly blocks = computed<any[]>(() => this.parse<any[]>(this.section().blocks, []));

  /** CountdownBar: Dd/Hh/Mm/Ss remaining, or null once expired. Computed synchronously in ngOnInit
   *  (works identically server + browser) so SSR output already shows correct numbers; a browser-only
   *  interval then keeps it ticking. */
  readonly remaining = signal<{ days: number; hours: number; mins: number; secs: number } | null>(null);
  private countdownTimer: ReturnType<typeof setInterval> | null = null;

  /** T15: click-to-select-in-canvas. Only active inside the theme editor's preview iframe —
   *  gated by ThemeService.editorMode() so real shoppers never see any of this. */
  @HostBinding('attr.data-section-id') get sectionIdAttr(): number { return this.section().pageSectionId; }
  @HostBinding('class.theme-editor-selected') highlighted = false;
  private readonly onEditorClick = (event: MouseEvent) => {
    event.preventDefault();
    event.stopPropagation();
    const target = event.target as HTMLElement;
    const blockEl = target.closest('[data-block-index]') as HTMLElement | null;
    const blockIndex = blockEl ? Number(blockEl.getAttribute('data-block-index')) : undefined;
    window.parent.postMessage({ type: 'theme-editor:select', sectionId: this.section().pageSectionId, blockIndex }, window.location.origin);
  };
  private readonly onWindowMessage = (event: MessageEvent) => {
    if (event.origin !== window.location.origin || event.data?.type !== 'theme-editor:highlight') return;
    this.highlighted = event.data.sectionId === this.section().pageSectionId;
  };

  ngOnInit(): void {
    const type = this.section().sectionType;
    if (type === 'Categories') {
      this.catalog.getCategories().subscribe((c) => this.categories.set(c.slice(0, 12)));
    } else if (type === 'FeaturedProducts' || type === 'ProductGrid') {
      const cfg: Record<string, any> = this.s();
      const count = Number(cfg['count']) || 8;
      if (cfg['source'] === 'collection' && cfg['collectionId']) {
        this.catalog.getCollectionMembers(Number(cfg['collectionId']), count).subscribe((items) => this.products.set(items));
      } else {
        this.catalog.getProducts({
          pageSize: count,
          isFeatured: cfg['source'] === 'featured' ? true : undefined,
          sort: cfg['source'] === 'newest' ? 'newest' : cfg['source'] === 'bestsellers' ? 'bestsellers' : undefined,
          categoryId: cfg['source'] === 'category' && cfg['categoryId'] ? Number(cfg['categoryId']) : undefined,
        }).subscribe((r) => this.products.set(r.items));
      }
    } else if (type === 'CountdownBar') {
      this.tickCountdown();
      if (this.isBrowser) this.countdownTimer = setInterval(() => this.tickCountdown(), 1000);
    }

    // Capture phase, not bubble: routerLink/href navigation must be intercepted before it fires,
    // not after — a bubble-phase listener on this host would run too late (RouterLink's own click
    // handler lives on the anchor itself and triggers navigation imperatively, not via the
    // browser's deferred default action, so preventDefault() from an ancestor bubble listener
    // wouldn't stop it).
    if (typeof window !== 'undefined' && this.theme.editorMode()) {
      this.elementRef.nativeElement.addEventListener('click', this.onEditorClick, { capture: true });
      window.addEventListener('message', this.onWindowMessage);
    }
  }

  ngOnDestroy(): void {
    if (this.countdownTimer) clearInterval(this.countdownTimer);
    if (typeof window === 'undefined') return;
    this.elementRef.nativeElement.removeEventListener('click', this.onEditorClick, { capture: true });
    window.removeEventListener('message', this.onWindowMessage);
  }

  private tickCountdown(): void {
    const target = new Date(this.s()['endDateTime'] ?? '').getTime();
    const diff = target - Date.now();
    if (!target || diff <= 0) {
      this.remaining.set(null);
      if (this.countdownTimer) { clearInterval(this.countdownTimer); this.countdownTimer = null; }
      return;
    }
    const secs = Math.floor(diff / 1000);
    this.remaining.set({ days: Math.floor(secs / 86400), hours: Math.floor(secs / 3600) % 24, mins: Math.floor(secs / 60) % 60, secs: secs % 60 });
  }

  stars(n: number): string { const r = Math.max(0, Math.min(5, Math.round(n || 0))); return '★★★★★'.slice(0, r) + '☆☆☆☆☆'.slice(0, 5 - r); }

  /** Tailwind column classes for TileGrid (full class names so the JIT keeps them). */
  tileCols(): string {
    const c = String(this.s()['columns'] ?? '3');
    return c === '2' ? 'grid-cols-1 sm:grid-cols-2' : c === '4' ? 'grid-cols-2 sm:grid-cols-4' : 'grid-cols-2 sm:grid-cols-3';
  }

  private parse<T>(json: string | null, fallback: T): T {
    try { return json ? JSON.parse(json) : fallback; } catch { return fallback; }
  }
}
