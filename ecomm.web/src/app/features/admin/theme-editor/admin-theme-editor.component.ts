import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, ElementRef, OnDestroy, OnInit, QueryList, ViewChild, ViewChildren, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CatalogService } from '../../../core/services/catalog.service';
import { BuilderPage, CmsService } from '../../../core/services/cms.service';
import { ThemeLibraryService } from '../../../core/services/theme-library.service';
import {
  BlockTypeSchema, SectionTypeSchema, ThemeAuthoringService, ThemeSectionAdmin, ThemeTemplateSummary,
} from '../../../core/services/theme-authoring.service';
import { SectionFieldComponent } from '../../../shared/section-field/section-field.component';
import { ColorScheme } from '../../../core/services/theme.service';

interface TemplateGroup { group: string; templates: ThemeTemplateSummary[]; }

/**
 * Theme editor (S4/S5): edits a specific theme (by route id — usually a Draft). Pick a page-type
 * template (or a Header/Footer/Announcement zone), add/reorder/configure its sections, and preview
 * the storefront rendered with THIS theme (via its preview token). Publish is done from the library.
 */
@Component({
  selector: 'app-admin-theme-editor',
  imports: [FormsModule, RouterLink, DragDropModule, SectionFieldComponent],
  template: `
    <div class="h-screen flex flex-col">
      <header class="h-12 bg-white border-b border-slate-200 flex items-center justify-between px-4 shrink-0">
        <div class="flex items-center gap-3 relative">
          <a routerLink="/admin/themes" class="text-slate-500 hover:text-slate-800 text-sm">← Themes</a>
          <span class="font-semibold text-slate-800">{{ themeName() || 'Theme editor' }}</span>
          <button type="button" (click)="toggleNavigator()" class="input text-sm py-1 flex items-center gap-1.5">
            {{ activeLabel() }} <span class="text-slate-400 text-xs">▾</span>
          </button>

          @if (navigatorOpen()) {
            <!-- Invisible full-screen backdrop closes the panel on outside click (same pattern as admin-orders' modal). -->
            <div class="fixed inset-0 z-40" (click)="navigatorOpen.set(false)"></div>
            <div class="absolute top-full left-24 mt-1 w-80 bg-white border border-slate-200 rounded-xl shadow-lg z-50 max-h-[70vh] overflow-auto" (click)="$event.stopPropagation()">
              <div class="p-2 sticky top-0 bg-white border-b border-slate-100">
                <input [ngModel]="navigatorSearch()" (ngModelChange)="navigatorSearch.set($event)" placeholder="Search online store" class="input w-full text-sm" autofocus />
              </div>
              @for (g of filteredGroups(); track g.group) {
                <div class="px-3 pt-2 pb-1 text-[11px] font-semibold text-slate-400 uppercase tracking-wide">{{ g.group }}</div>
                @for (t of g.templates; track t.templateKey) {
                  <button type="button" (click)="selectTemplate(t.templateKey); navigatorOpen.set(false)"
                          class="w-full text-left px-3 py-1.5 text-sm hover:bg-slate-50 flex items-center justify-between"
                          [class.text-slate-900]="t.templateKey === activeKey()" [class.font-medium]="t.templateKey === activeKey()">
                    <span>{{ t.label }}</span>
                    @if (t.sectionCount) { <span class="text-xs text-slate-400">{{ t.sectionCount }}</span> }
                  </button>
                }
              }
              @if (filteredPages().length) {
                <div class="px-3 pt-2 pb-1 text-[11px] font-semibold text-slate-400 uppercase tracking-wide border-t border-slate-100 mt-1">Pages</div>
                @for (p of filteredPages(); track p.pageId) {
                  <a [routerLink]="['/admin/pages', p.pageId, 'build']" (click)="navigatorOpen.set(false)"
                     class="block px-3 py-1.5 text-sm hover:bg-slate-50 truncate">{{ p.title }}</a>
                }
              }
              @if (matchesSearch('Products')) {
                <a routerLink="/admin/products" class="block px-3 py-1.5 text-sm hover:bg-slate-50 border-t border-slate-100 mt-1">Products →</a>
              }
              @if (matchesSearch('Collections')) {
                <a routerLink="/admin/categories" class="block px-3 py-1.5 text-sm hover:bg-slate-50">Collections →</a>
              }
            </div>
          }
        </div>
        <div class="flex items-center gap-2">
          @if (message()) { <span class="text-xs text-green-600">{{ message() }}</span> }
          <div class="flex rounded-lg border border-slate-300 overflow-hidden text-sm">
            <button type="button" (click)="device.set('desktop')" title="Desktop preview"
                    class="px-2.5 py-1" [class]="device() === 'desktop' ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">🖥</button>
            <button type="button" (click)="device.set('mobile')" title="Mobile preview"
                    class="px-2.5 py-1" [class]="device() === 'mobile' ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">📱</button>
          </div>
          <button type="button" (click)="toggleSettings()" class="text-sm px-3 py-1 rounded-lg border border-slate-300 hover:bg-slate-50"
                  [class.bg-slate-100]="settingsMode()">⚙ Theme settings</button>
          <button type="button" (click)="reloadPreview()" class="text-sm px-3 py-1 rounded-lg border border-slate-300 hover:bg-slate-50">↻ Preview</button>
        </div>
      </header>

      <div class="flex-1 flex min-h-0">
        <!-- left: section list -->
        <aside class="w-64 bg-slate-50 border-r border-slate-200 overflow-auto p-3 shrink-0">
          <p class="text-xs text-slate-400 mb-2">Sections on <span class="font-medium text-slate-600">{{ activeLabel() }}</span></p>
          @if (sections().length === 0) {
            <p class="text-xs text-slate-400 italic mb-2">No sections yet — this page uses the built-in default layout until you add one.</p>
          }
          <div cdkDropList (cdkDropListDropped)="drop($event)" class="space-y-1">
            @for (s of sections(); track s.id) {
              <div cdkDrag class="bg-white border rounded-lg px-2 py-2 text-sm cursor-move flex items-center justify-between"
                   [class]="s.id === selectedId() ? 'border-blue-500 ring-1 ring-blue-200' : 'border-slate-200'"
                   (click)="select(s)">
                <span class="truncate" [class.text-slate-400]="!s.isVisible">⋮⋮ {{ s.title || s.sectionType }}</span>
                <span class="text-xs text-slate-300">{{ s.kind }}</span>
              </div>
            }
          </div>
          <div class="mt-3">
            <select #picker class="input w-full text-sm">
              <option value="">+ Add section…</option>
              @for (t of types(); track t.key) { <option [value]="t.key">{{ t.label }}</option> }
            </select>
            <button type="button" (click)="addSection(picker.value); picker.value=''" class="btn-primary w-full mt-2 text-sm">Add</button>
            @if (pendingInsertBefore(); as pi) {
              <p class="text-xs text-blue-600 mt-1.5">
                Adding above “{{ pi.title || pi.sectionType }}” —
                <button type="button" class="underline" (click)="pendingInsertBefore.set(null)">cancel</button>
              </p>
            }
          </div>
        </aside>

        <!-- center: live preview (framed to 390px in mobile mode) -->
        <main class="flex-1 bg-slate-100 min-w-0" [class.py-3]="device() === 'mobile'">
          <div class="h-full mx-auto" [class]="device() === 'mobile' ? 'max-w-[390px] rounded-xl border border-slate-300 shadow-lg overflow-hidden bg-white' : 'w-full'">
            @if (previewUrl()) { <iframe #previewFrame [src]="previewUrl()" class="w-full h-full border-0" title="preview"></iframe> }
          </div>
        </main>

        <!-- right: theme settings, or settings for the selected section -->
        <aside class="w-80 bg-white border-l border-slate-200 overflow-auto p-4 shrink-0">
          @if (settingsMode()) {
            <h2 class="font-semibold text-slate-800 mb-3">Theme settings</h2>
            <label class="block mb-3"><span class="lbl">Store name</span><input [(ngModel)]="themeSettings['StoreName']" class="input w-full" /></label>
            <label class="block mb-3"><span class="lbl">Logo URL</span><input [(ngModel)]="themeSettings['Logo']" class="input w-full" placeholder="https://…/logo.png" /></label>
            <div class="grid grid-cols-2 gap-3">
              <label class="block mb-3"><span class="lbl">Primary colour</span><input type="color" [(ngModel)]="themeSettings['PrimaryColor']" class="input h-9 w-full" /></label>
              <label class="block mb-3"><span class="lbl">Secondary colour</span><input type="color" [(ngModel)]="themeSettings['SecondaryColor']" class="input h-9 w-full" /></label>
            </div>
            <label class="block mb-3"><span class="lbl">Body font</span>
              <select [(ngModel)]="themeSettings['Font']" class="input w-full">
                @for (f of fonts; track f) { <option [value]="f">{{ f }}</option> }
              </select></label>
            <label class="block mb-3"><span class="lbl">Heading font</span>
              <select [(ngModel)]="themeSettings['HeadingFont']" class="input w-full">
                <option value="">Same as body</option>
                @for (f of fonts; track f) { <option [value]="f">{{ f }}</option> }
              </select></label>
            <div class="grid grid-cols-2 gap-3">
              <label class="block mb-3"><span class="lbl">Accent colour</span><input type="color" [(ngModel)]="themeSettings['AccentColor']" class="input h-9 w-full" /></label>
              <label class="block mb-3"><span class="lbl">Button style</span>
                <select [(ngModel)]="themeSettings['ButtonStyle']" class="input w-full">
                  <option value="rounded">Rounded</option><option value="pill">Pill</option><option value="square">Square</option>
                </select></label>
            </div>

            <div class="border-t border-slate-100 pt-3 mt-1 mb-3">
              <div class="flex items-center justify-between mb-2">
                <span class="lbl mb-0">Colour schemes</span>
                <button type="button" (click)="addScheme()" class="text-xs text-blue-600 hover:underline">+ Add</button>
              </div>
              <p class="text-xs text-slate-400 mb-2">Named palettes sections can opt into (e.g. a dark promo band).</p>
              @for (s of schemes; track s.key) {
                <div class="border border-slate-200 rounded-lg p-2 mb-2">
                  <div class="flex justify-between items-center mb-1">
                    <input [(ngModel)]="s.name" class="input text-sm flex-1 mr-2" placeholder="Scheme name" />
                    <button type="button" (click)="removeScheme(s)" class="text-red-500 text-xs">×</button>
                  </div>
                  <div class="grid grid-cols-4 gap-1">
                    <label class="text-center"><span class="text-[10px] text-slate-400 block">Bg</span><input type="color" [(ngModel)]="s.background" class="input h-8 w-full" /></label>
                    <label class="text-center"><span class="text-[10px] text-slate-400 block">Text</span><input type="color" [(ngModel)]="s.text" class="input h-8 w-full" /></label>
                    <label class="text-center"><span class="text-[10px] text-slate-400 block">Button</span><input type="color" [(ngModel)]="s.button" class="input h-8 w-full" /></label>
                    <label class="text-center"><span class="text-[10px] text-slate-400 block">Border</span><input type="color" [(ngModel)]="s.border" class="input h-8 w-full" /></label>
                  </div>
                </div>
              }
            </div>
            <div class="grid grid-cols-2 gap-3">
              <label class="block mb-3"><span class="lbl">Corners</span>
                <select [(ngModel)]="themeSettings['Radius']" class="input w-full">
                  <option value="soft">Soft</option><option value="sharp">Sharp</option><option value="round">Rounded</option>
                </select></label>
              <label class="block mb-3"><span class="lbl">Card style</span>
                <select [(ngModel)]="themeSettings['CardStyle']" class="input w-full">
                  <option value="bordered">Bordered</option><option value="shadow">Shadow</option><option value="elevated">Elevated</option><option value="flat">Flat</option>
                </select></label>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <label class="block mb-3"><span class="lbl">Density</span>
                <select [(ngModel)]="themeSettings['Density']" class="input w-full">
                  <option value="cozy">Cozy</option><option value="compact">Compact</option><option value="spacious">Spacious</option>
                </select></label>
              <label class="block mb-3"><span class="lbl">Heading case</span>
                <select [(ngModel)]="themeSettings['HeadingTransform']" class="input w-full">
                  <option value="none">Normal</option><option value="uppercase">UPPERCASE</option>
                </select></label>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <label class="block mb-3"><span class="lbl">Base font size (px)</span><input type="number" [(ngModel)]="themeSettings['BaseFontSize']" class="input w-full" placeholder="16" /></label>
              <label class="block mb-3"><span class="lbl">Max width (px)</span><input type="number" [(ngModel)]="themeSettings['ContainerWidth']" class="input w-full" placeholder="1480" /></label>
            </div>
            <label class="block mb-3"><span class="lbl">Favicon URL</span><input [(ngModel)]="themeSettings['Favicon']" class="input w-full" placeholder="https://…/favicon.png" /></label>
            <button type="button" (click)="saveSettings()" [disabled]="saving()" class="btn-primary w-full mt-2">{{ saving() ? 'Saving…' : 'Save theme settings' }}</button>
          } @else if (selected(); as sec) {
            <div class="flex items-center justify-between mb-3">
              <h2 class="font-semibold text-slate-800">{{ schema()?.label ?? sec.sectionType }}</h2>
              <div class="flex gap-2 text-xs">
                <button type="button" (click)="toggleHide(sec)" class="text-slate-500 hover:underline">{{ sec.isVisible ? 'Hide' : 'Show' }}</button>
                <button type="button" (click)="duplicate(sec)" class="text-slate-500 hover:underline">Duplicate</button>
                <button type="button" (click)="remove(sec)" class="text-red-500 hover:underline">Delete</button>
              </div>
            </div>

            @for (f of schema()?.settings ?? []; track f.key) {
              <label class="block mb-3">
                <span class="lbl">{{ f.label }}</span>
                <app-section-field [schema]="f" [(value)]="settingsObj[f.key]" [colorSchemes]="schemes" (valueChange)="onFieldEdit()" />
                @if (f.help) { <span class="text-xs text-slate-400">{{ f.help }}</span> }
              </label>
            }

            @if (blockType(); as bt) {
              <div class="border-t border-slate-100 pt-3 mt-3">
                <div class="flex items-center justify-between mb-2"><span class="lbl mb-0">{{ bt.label }}s</span>
                  <button type="button" (click)="addBlock(bt)" class="text-xs text-blue-600 hover:underline">+ Add</button></div>
                @for (b of blocksArr; track $index) {
                  <div #blockCard class="border rounded-lg p-2 mb-2" [class]="$index === selectedBlockIndex() ? 'border-blue-500 ring-2 ring-blue-200' : 'border-slate-200'">
                    <div class="flex justify-between text-xs text-slate-400 mb-1"><span>{{ bt.label }} {{ $index + 1 }}</span>
                      <button type="button" (click)="removeBlock($index)" class="text-red-500">×</button></div>
                    @for (f of bt.fields; track f.key) {
                      <label class="block mb-1"><span class="text-xs text-slate-500">{{ f.label }}</span>
                        <app-section-field [schema]="f" [(value)]="b[f.key]" [colorSchemes]="schemes" (valueChange)="onFieldEdit()" />
                      </label>
                    }
                  </div>
                }
              </div>
            }

            <div class="sticky bottom-0 bg-white border-t border-slate-100 -mx-4 px-4 pt-2 pb-1 mt-3">
              @if (dirty()) { <p class="text-[11px] text-amber-600 mb-1">Unsaved changes — shown live in the preview, saved when you click Save</p> }
              <button type="button" (click)="save(sec)" [disabled]="saving()" class="btn-primary w-full">{{ saving() ? 'Saving…' : 'Save section' }}</button>
            </div>
          } @else {
            <div class="text-slate-400 text-sm text-center p-8">Select or add a section to edit it.</div>
          }
        </aside>
      </div>
    </div>
  `,
})
export class AdminThemeEditorComponent implements OnInit, OnDestroy {
  private readonly svc = inject(ThemeAuthoringService);
  private readonly library = inject(ThemeLibraryService);
  private readonly catalog = inject(CatalogService);
  private readonly cms = inject(CmsService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly route = inject(ActivatedRoute);

  @ViewChild('previewFrame') previewFrame?: ElementRef<HTMLIFrameElement>;
  @ViewChildren('blockCard') blockCards?: QueryList<ElementRef<HTMLElement>>;

  readonly templates = signal<ThemeTemplateSummary[]>([]);
  readonly sections = signal<ThemeSectionAdmin[]>([]);
  readonly types = signal<SectionTypeSchema[]>([]);
  readonly activeKey = signal<string>('index');
  readonly selectedId = signal<number | null>(null);
  /** T15: which block card is highlighted after a click-to-select-in-canvas from the preview iframe. */
  readonly selectedBlockIndex = signal<number | null>(null);
  /** E2: unsaved edits exist for the selected section (already streamed live to the canvas). */
  readonly dirty = signal(false);
  /** E1: set by the canvas "+ Add section" pill — where the next added section should land. */
  readonly pendingInsertBefore = signal<ThemeSectionAdmin | null>(null);
  /** E1: preview device frame. */
  readonly device = signal<'desktop' | 'mobile'>('desktop');
  private draftTimer: ReturnType<typeof setTimeout> | null = null;
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly previewUrl = signal<SafeResourceUrl | null>(null);
  readonly themeName = signal<string>('');
  readonly settingsMode = signal(false);

  settingsObj: Record<string, any> = {};
  blocksArr: Record<string, any>[] = [];
  themeSettings: Record<string, string> = {};
  schemes: ColorScheme[] = [];
  readonly fonts = [
    'Inter', 'Poppins', 'Roboto', 'Montserrat', 'Lato', 'Open Sans', 'DM Sans', 'Work Sans', 'Nunito',
    'Space Grotesk', 'Archivo', 'Oswald', 'Bebas Neue', 'Playfair Display', 'Cormorant Garamond', 'Lora',
  ];
  private sampleProductSlug = '';
  private themeId = 0;
  private previewToken: string | null = null;

  readonly grouped = computed<TemplateGroup[]>(() => {
    const order = ['Header', 'Templates', 'Footer'];
    const byGroup = new Map<string, ThemeTemplateSummary[]>();
    for (const t of this.templates()) { const l = byGroup.get(t.group) ?? []; l.push(t); byGroup.set(t.group, l); }
    return order.filter((g) => byGroup.has(g)).map((g) => ({ group: g, templates: byGroup.get(g)! }));
  });
  readonly activeLabel = computed(() => this.templates().find((t) => t.templateKey === this.activeKey())?.label ?? this.activeKey());

  /** Page navigator (Shopify-parity "Home page ▾"): search across templates + real content pages,
   *  plus jump-off shortcuts to Products/Collections — those aren't theme-authored, so they leave the
   *  editor for the existing catalog admin screens rather than trying to enumerate every item here. */
  readonly navigatorOpen = signal(false);
  readonly navigatorSearch = signal('');
  readonly pages = signal<BuilderPage[]>([]);
  toggleNavigator(): void {
    if (!this.navigatorOpen() && this.pages().length === 0) this.cms.listPages().subscribe((p) => this.pages.set(p));
    this.navigatorSearch.set('');
    this.navigatorOpen.update((v) => !v);
  }
  matchesSearch(label: string): boolean {
    const q = this.navigatorSearch().trim().toLowerCase();
    return !q || label.toLowerCase().includes(q);
  }
  readonly filteredGroups = computed<TemplateGroup[]>(() => {
    const q = this.navigatorSearch().trim().toLowerCase();
    if (!q) return this.grouped();
    return this.grouped()
      .map((g) => ({ group: g.group, templates: g.templates.filter((t) => t.label.toLowerCase().includes(q)) }))
      .filter((g) => g.templates.length > 0);
  });
  readonly filteredPages = computed<BuilderPage[]>(() => {
    const q = this.navigatorSearch().trim().toLowerCase();
    return q ? this.pages().filter((p) => p.title.toLowerCase().includes(q)) : this.pages();
  });

  ngOnInit(): void {
    this.themeId = Number(this.route.snapshot.paramMap.get('themeId'));
    this.catalog.getProducts({ pageSize: 1 }).subscribe((r) => { this.sampleProductSlug = r.items[0]?.slug ?? ''; });
    this.library.get(this.themeId).subscribe((t) => { this.themeName.set(t.name); this.previewToken = t.previewToken; this.setPreview(); });
    this.svc.templates(this.themeId).subscribe((t) => this.templates.set(t));
    this.svc.getSettings(this.themeId).subscribe((s) => {
      this.themeSettings = { ...s };
      this.schemes = this.parse(s['ColorSchemes'] ?? null, [] as ColorScheme[]);
    });
    this.selectTemplate('index');
    if (typeof window !== 'undefined') window.addEventListener('message', this.onWindowMessage);
  }

  ngOnDestroy(): void {
    if (this.draftTimer) clearTimeout(this.draftTimer);
    if (typeof window !== 'undefined') window.removeEventListener('message', this.onWindowMessage);
  }

  /** T15/E1: a merchant clicked a section/block, a toolbar action, or an insert pill in the preview. */
  private readonly onWindowMessage = (event: MessageEvent): void => {
    if (typeof window === 'undefined' || event.origin !== window.location.origin) return;
    const data = event.data;
    if (data?.type === 'theme-editor:select') {
      const sec = this.sections().find((x) => x.id === data.sectionId);
      if (!sec || !this.select(sec)) return;   // not in the active template, or a dirty draft was kept
      this.selectedBlockIndex.set(typeof data.blockIndex === 'number' ? data.blockIndex : null);
      this.postHighlight(sec.id, this.selectedBlockIndex());
      if (typeof data.blockIndex === 'number') {
        queueMicrotask(() => this.blockCards?.get(data.blockIndex)?.nativeElement.scrollIntoView({ block: 'nearest' }));
      }
    } else if (data?.type === 'theme-editor:action') {
      const sec = this.sections().find((x) => x.id === data.sectionId);
      if (!sec) return;
      if (data.action === 'toggleHide') this.toggleHide(sec);
      else if (data.action === 'duplicate') this.duplicate(sec);
      else if (data.action === 'delete') this.remove(sec);
    } else if (data?.type === 'theme-editor:insert') {
      const sec = this.sections().find((x) => x.id === data.beforeSectionId);
      if (sec) this.pendingInsertBefore.set(sec);
    } else if (data?.type === 'theme-editor:ready') {
      // The preview iframe just (re)loaded a fresh document — every reload replaces it entirely, so
      // whatever was highlighted before is gone until we re-send it. Re-assert the current selection
      // now that the new document's listener has confirmed it's actually there to receive it.
      if (this.selectedId() !== null) this.postHighlight(this.selectedId(), this.selectedBlockIndex());
    }
  };

  private postToPreview(message: unknown): void {
    this.previewFrame?.nativeElement.contentWindow?.postMessage(message, window.location.origin);
  }

  /** Mirror the current selection into the preview so it outlines the section (and block, when set). */
  private postHighlight(sectionId: number | null, blockIndex: number | null = null): void {
    this.postToPreview({ type: 'theme-editor:highlight', sectionId, blockIndex });
  }

  /** E2: any field edit → mark dirty + debounce-stream the draft into the canvas (live preview). */
  onFieldEdit(): void {
    this.dirty.set(true);
    if (this.draftTimer) clearTimeout(this.draftTimer);
    this.draftTimer = setTimeout(() => this.postDraft(), 250);
  }

  private postDraft(): void {
    const sec = this.selected();
    if (!sec) return;
    this.postToPreview({
      type: 'theme-editor:update-section', sectionId: sec.id,
      settings: JSON.stringify(this.settingsObj), blocks: JSON.stringify(this.blocksArr),
    });
  }

  /** E2: guard against silently losing a dirty draft; reverts the canvas to last-saved on discard. */
  private confirmDiscard(): boolean {
    if (!this.dirty()) return true;
    if (typeof window !== 'undefined' && !window.confirm('Discard unsaved changes to this section?')) return false;
    const prev = this.selected();
    if (prev) this.postToPreview({ type: 'theme-editor:update-section', sectionId: prev.id, settings: prev.settings, blocks: prev.blocks });
    this.dirty.set(false);
    return true;
  }

  toggleSettings(): void { this.settingsMode.update((v) => !v); }

  saveSettings(): void {
    this.saving.set(true);
    this.themeSettings['ColorSchemes'] = JSON.stringify(this.schemes);
    this.svc.saveSettings(this.themeId, this.themeSettings).subscribe({
      next: (s) => { this.themeSettings = { ...s }; this.schemes = this.parse(s['ColorSchemes'] ?? null, [] as ColorScheme[]); this.saving.set(false); this.toast('Theme settings saved.'); this.reloadPreview(); },
      error: () => this.saving.set(false),
    });
  }

  addScheme(): void {
    this.schemes = [...this.schemes, { key: `scheme-${Date.now()}`, name: 'New scheme', background: '#111827', text: '#ffffff', button: '#2563eb', border: '#334155' }];
  }
  removeScheme(s: ColorScheme): void { this.schemes = this.schemes.filter((x) => x.key !== s.key); }

  selectTemplate(key: string): void {
    if (!this.confirmDiscard()) return;
    this.activeKey.set(key);
    this.selectedId.set(null);
    this.svc.sectionTypes(key).subscribe((t) => this.types.set(t));
    this.loadSections();
    this.setPreview();
  }

  private loadSections(): void {
    this.svc.sections(this.themeId, this.activeKey()).subscribe((s) => {
      this.sections.set(s);
      if (this.selectedId() && !s.some((x) => x.id === this.selectedId())) this.selectedId.set(null);
      this.svc.templates(this.themeId).subscribe((t) => this.templates.set(t));   // refresh section counts
    });
  }

  selected(): ThemeSectionAdmin | null { return this.sections().find((s) => s.id === this.selectedId()) ?? null; }
  schema(): SectionTypeSchema | null { const s = this.selected(); return s ? this.types().find((t) => t.key === s.sectionType) ?? null : null; }
  blockType(): BlockTypeSchema | null { return this.schema()?.blockTypes?.[0] ?? null; }

  select(s: ThemeSectionAdmin): boolean {
    if (!this.confirmDiscard()) return false;
    this.selectedId.set(s.id);
    this.settingsObj = this.parse(s.settings, {});
    this.blocksArr = this.parse(s.blocks, []);
    this.selectedBlockIndex.set(null);
    this.postHighlight(s.id);
    return true;
  }

  addSection(type: string): void {
    if (!type) return;
    this.svc.addSection(this.themeId, this.activeKey(), type).subscribe((sec) => {
      const before = this.pendingInsertBefore();
      this.pendingInsertBefore.set(null);
      // select() runs before the reload lands — harmless; the 'theme-editor:ready' handshake
      // (fired when the reloaded iframe's new document mounts) re-asserts the highlight reliably.
      const finish = () => { this.loadSections(); this.select(sec); this.reloadPreview(); };
      if (before) {
        // E1: canvas "+ Add section" — place the new section above the one whose pill was clicked.
        const ids = this.sections().map((s) => s.id).filter((id) => id !== sec.id);
        const idx = ids.indexOf(before.id);
        ids.splice(idx < 0 ? ids.length : idx, 0, sec.id);
        this.svc.reorder(this.themeId, this.activeKey(), ids).subscribe(finish);
      } else {
        finish();
      }
    });
  }

  drop(e: CdkDragDrop<ThemeSectionAdmin[]>): void {
    const arr = [...this.sections()];
    moveItemInArray(arr, e.previousIndex, e.currentIndex);
    this.sections.set(arr);
    this.svc.reorder(this.themeId, this.activeKey(), arr.map((s) => s.id)).subscribe(() => this.reloadPreview());
  }

  save(sec: ThemeSectionAdmin): void {
    this.saving.set(true);
    this.svc.updateSection(sec.id, {
      title: sec.title, settings: JSON.stringify(this.settingsObj), blocks: JSON.stringify(this.blocksArr),
      isVisible: sec.isVisible, startsAt: null, endsAt: null,
    }).subscribe(() => {
      this.saving.set(false);
      this.dirty.set(false);
      this.toast('Saved.');
      this.loadSections();
      // E2: no iframe reload — the canvas already renders this data live; re-post the saved values
      // so its override matches what was persisted. Structural ops (add/delete/reorder/hide/template
      // switch) still reload, since page composition comes from the server list.
      this.postDraft();
    });
  }

  toggleHide(sec: ThemeSectionAdmin): void {
    this.svc.updateSection(sec.id, { title: sec.title, settings: sec.settings, blocks: sec.blocks, isVisible: !sec.isVisible, startsAt: null, endsAt: null })
      .subscribe(() => { this.loadSections(); this.reloadPreview(); });
  }
  duplicate(sec: ThemeSectionAdmin): void { this.svc.duplicateSection(sec.id).subscribe(() => { this.loadSections(); this.reloadPreview(); }); }
  remove(sec: ThemeSectionAdmin): void {
    if (typeof window !== 'undefined' && !window.confirm('Delete this section?')) return;
    this.svc.deleteSection(sec.id).subscribe(() => { this.selectedId.set(null); this.loadSections(); this.reloadPreview(); });
  }

  addBlock(bt: BlockTypeSchema): void {
    const b: Record<string, any> = {};
    for (const f of bt.fields) b[f.key] = f.default ?? '';
    this.blocksArr = [...this.blocksArr, b];
    this.onFieldEdit();
  }
  removeBlock(i: number): void {
    this.blocksArr = this.blocksArr.filter((_, idx) => idx !== i);
    this.onFieldEdit();
  }

  reloadPreview(): void { this.setPreview(true); }
  private setPreview(bust = false): void {
    let url = this.previewPath(this.activeKey());
    // Render the storefront with THIS theme (draft or published) via its preview token.
    if (this.previewToken) url += `${url.includes('?') ? '&' : '?'}preview=${this.previewToken}`;
    if (bust) url += `${url.includes('?') ? '&' : '?'}_=${Math.floor(performance.now())}`;
    this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
  }

  /** Storefront route that best represents a template (for the preview iframe). */
  private previewPath(key: string): string {
    switch (key) {
      case 'product': return this.sampleProductSlug ? `/product/${this.sampleProductSlug}` : '/products';
      case 'collection': case 'list-collections': return '/products';
      case 'search': return '/products?search=a';
      case 'cart': return '/cart';
      case 'password': return '/password';
      case 'account': return '/account';
      case '404': return '/__theme_preview_404';
      default: return '/';   // index + header/footer/announcement zones show on the home page
    }
  }

  private parse(json: string | null, fallback: any): any { try { return json ? JSON.parse(json) : fallback; } catch { return fallback; } }
  private toast(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 2500); }
}
