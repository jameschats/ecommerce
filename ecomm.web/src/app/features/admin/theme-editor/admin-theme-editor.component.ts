import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
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
 * Theme editor (S4/S5, E3): edits a specific theme (by route id — usually a Draft). Pick a page-type
 * template (or a Header/Footer/Announcement zone), add/reorder/configure its sections and blocks in
 * a Shopify-style tree, and preview the storefront rendered with THIS theme (via its preview token).
 * Publish is done from the library.
 *
 * E3: the left sidebar is now an expandable tree — the selected section's blocks list directly below
 * it (dynamic titles), not a second flat list. The right panel is "focused": a section row shows only
 * that section's settings, a block row shows only that block's fields — never both bundled together.
 * Only the SELECTED section's blocks are ever loaded/shown (settingsObj/blocksArr are still singular,
 * not a per-section map) — a deliberate scope line, not an oversight: true independent multi-section
 * expansion would need every expanded section's blocks loaded at once, a bigger data-shape change for
 * marginal extra value over "the section you're editing shows its blocks".
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
              @if (matchesSearch('Categories')) {
                <a routerLink="/admin/categories" class="block px-3 py-1.5 text-sm hover:bg-slate-50">Categories →</a>
              }
              @if (matchesSearch('Collections')) {
                <a routerLink="/admin/collections" class="block px-3 py-1.5 text-sm hover:bg-slate-50">Collections →</a>
              }
            </div>
          }

          <!-- E5 remainder: choose which real product/collection a dynamic template previews with. -->
          @if (activeKey() === 'product' || activeKey() === 'collection') {
            <button type="button" (click)="togglePreviewPicker()" class="input text-sm py-1 flex items-center gap-1.5 max-w-[220px] truncate">
              Previewing: {{ previewContextLabel() }} <span class="text-slate-400 text-xs">▾</span>
            </button>
            @if (previewPickerOpen()) {
              <div class="fixed inset-0 z-40" (click)="previewPickerOpen.set(false)"></div>
              <div class="absolute top-full left-0 mt-1 w-72 bg-white border border-slate-200 rounded-xl shadow-lg z-50 p-2" (click)="$event.stopPropagation()">
                @if (activeKey() === 'product') {
                  <input [(ngModel)]="previewQuery" (ngModelChange)="previewSearch$.next($event)" placeholder="Search products…" class="input w-full text-sm" autofocus />
                  <div class="max-h-56 overflow-auto mt-1">
                    @for (p of previewProductResults(); track p.productId) {
                      <button type="button" (click)="pickPreviewProduct(p)" class="block w-full text-left px-2 py-1.5 text-sm hover:bg-slate-50 rounded">{{ p.name }}</button>
                    }
                  </div>
                } @else {
                  <div class="max-h-56 overflow-auto">
                    <button type="button" (click)="pickPreviewCategory(null)" class="block w-full text-left px-2 py-1.5 text-sm hover:bg-slate-50 rounded">All products</button>
                    @for (c of previewCategories(); track c.categoryId) {
                      <button type="button" (click)="pickPreviewCategory(c)" class="block w-full text-left px-2 py-1.5 text-sm hover:bg-slate-50 rounded">{{ c.name }}</button>
                    }
                  </div>
                }
              </div>
            }
          }
        </div>
        <div class="flex items-center gap-2">
          @if (message()) { <span class="text-xs text-green-600">{{ message() }}</span> }
          <div class="flex rounded-lg border border-slate-300 overflow-hidden text-sm">
            <button type="button" (click)="undo()" [disabled]="!canUndo()" title="Undo (Ctrl+Z)"
                    class="px-2.5 py-1 text-slate-600 hover:bg-slate-50 disabled:opacity-30 disabled:hover:bg-transparent border-r border-slate-300">↶</button>
            <button type="button" (click)="redo()" [disabled]="!canRedo()" title="Redo (Ctrl+Y)"
                    class="px-2.5 py-1 text-slate-600 hover:bg-slate-50 disabled:opacity-30 disabled:hover:bg-transparent">↷</button>
          </div>
          <div class="flex rounded-lg border border-slate-300 overflow-hidden text-sm">
            <button type="button" (click)="device.set('desktop')" title="Desktop preview"
                    class="px-2.5 py-1" [class]="device() === 'desktop' ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">🖥</button>
            <button type="button" (click)="device.set('mobile')" title="Mobile preview"
                    class="px-2.5 py-1" [class]="device() === 'mobile' ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">📱</button>
          </div>
          <button type="button" (click)="toggleInspector()" title="Toggle inspector — click through to real links/buttons in the canvas"
                  class="text-sm px-3 py-1 rounded-lg border border-slate-300 hover:bg-slate-50" [class.bg-slate-100]="inspectorMode()">👁 Inspect</button>
          <button type="button" (click)="toggleFullscreen()" class="text-sm px-3 py-1 rounded-lg border border-slate-300 hover:bg-slate-50">
            {{ fullscreen() ? '⤡ Exit fullscreen' : '⤢ Fullscreen' }}
          </button>
          <button type="button" (click)="toggleSettings()" class="text-sm px-3 py-1 rounded-lg border border-slate-300 hover:bg-slate-50"
                  [class.bg-slate-100]="settingsMode()">⚙ Theme settings</button>
          <button type="button" (click)="reloadPreview()" class="text-sm px-3 py-1 rounded-lg border border-slate-300 hover:bg-slate-50">↻ Preview</button>
        </div>
      </header>

      <div class="flex-1 flex min-h-0">
        <!-- left: section/block tree -->
        @if (!fullscreen()) {
        <aside class="w-64 bg-slate-50 border-r border-slate-200 overflow-auto p-3 shrink-0">
          <p class="text-xs text-slate-400 mb-2">Sections on <span class="font-medium text-slate-600">{{ activeLabel() }}</span></p>
          @if (sections().length === 0) {
            <p class="text-xs text-slate-400 italic mb-2">No sections yet — this page uses the built-in default layout until you add one.</p>
          }
          <div cdkDropList (cdkDropListDropped)="drop($event)" class="space-y-1">
            @for (s of sections(); track s.id) {
              <div>
                <div cdkDrag class="bg-white border rounded-lg px-2 py-2 text-sm cursor-move flex items-center justify-between"
                     [attr.data-tree-section]="s.id"
                     [class]="s.id === selectedId() && focusedBlockIndex() === null ? 'border-blue-500 ring-1 ring-blue-200' : 'border-slate-200'"
                     (click)="selectSection(s)">
                  <span class="truncate flex items-center gap-1" [class.text-slate-400]="!s.isVisible">
                    @if (s.id === selectedId()) { <span class="text-slate-400 text-[10px]">▾</span> } @else { <span class="text-slate-300 text-[10px]">▸</span> }
                    ⋮⋮ {{ s.title || s.sectionType }}
                  </span>
                  <span class="text-xs text-slate-300">{{ s.kind }}</span>
                </div>
                @if (s.id === selectedId() && blockTypes().length > 0) {
                  <div cdkDropList [cdkDropListData]="blocksArr" (cdkDropListDropped)="dropBlock($event)" class="ml-4 mt-1 space-y-1">
                    @for (b of blocksArr; track $index) {
                      <div cdkDrag [attr.data-tree-block]="$index"
                           class="bg-white border rounded-lg px-2 py-1.5 text-xs cursor-move truncate"
                           [class]="$index === focusedBlockIndex() ? 'border-blue-500 ring-1 ring-blue-200 text-slate-800' : 'border-slate-200 text-slate-500'"
                           (click)="$event.stopPropagation(); selectBlock($index)">
                        {{ blockTitle(b, $index) }}
                      </div>
                    }
                  </div>
                  @if (blockTypes().length === 1) {
                    <button type="button" (click)="$event.stopPropagation(); addBlock(blockTypes()[0])" class="ml-4 mt-1 text-xs text-blue-600 hover:underline">+ Add {{ blockTypes()[0].label }}</button>
                  } @else {
                    <!-- T17: a section with multiple block types (e.g. CustomSection's Heading/Text/Image/
                         Button/Spacer/Divider palette) gets a picker menu instead of one fixed button. -->
                    <div class="ml-4 mt-1 relative">
                      <button type="button" (click)="$event.stopPropagation(); addBlockMenuOpen.set(addBlockMenuOpen() === s.id ? null : s.id)" class="text-xs text-blue-600 hover:underline">+ Add block</button>
                      @if (addBlockMenuOpen() === s.id) {
                        <div class="fixed inset-0 z-40" (click)="addBlockMenuOpen.set(null)"></div>
                        <div class="absolute left-0 top-full mt-1 w-36 bg-white border border-slate-200 rounded-lg shadow-lg z-50 py-1" (click)="$event.stopPropagation()">
                          @for (bt of blockTypes(); track bt.key) {
                            <button type="button" (click)="addBlock(bt); addBlockMenuOpen.set(null)" class="block w-full text-left px-3 py-1.5 text-xs hover:bg-slate-50">{{ bt.label }}</button>
                          }
                        </div>
                      }
                    </div>
                  }
                }
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
                Adding above "{{ pi.title || pi.sectionType }}" —
                <button type="button" class="underline" (click)="pendingInsertBefore.set(null)">cancel</button>
              </p>
            }
          </div>
        </aside>
        }

        <!-- center: live preview (framed to 390px in mobile mode) -->
        <main class="flex-1 bg-slate-100 min-w-0" [class.py-3]="device() === 'mobile'">
          <div class="h-full mx-auto" [class]="device() === 'mobile' ? 'max-w-[390px] rounded-xl border border-slate-300 shadow-lg overflow-hidden bg-white' : 'w-full'">
            @if (previewUrl()) { <iframe #previewFrame [src]="previewUrl()" class="w-full h-full border-0" title="preview"></iframe> }
          </div>
        </main>

        <!-- right: theme settings, or the focused section/block panel -->
        @if (!fullscreen()) {
        <aside #rightPanel class="w-80 bg-white border-l border-slate-200 overflow-auto p-4 shrink-0">
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
            @if (focusedBlockIndex() === null) {
              <!-- Section-focused panel: settings only — its blocks live in the tree, not duplicated here. -->
              <div class="flex items-center justify-between mb-3">
                <h2 class="font-semibold text-slate-800">{{ schema()?.label ?? sec.sectionType }}</h2>
                <div class="flex gap-2 text-xs">
                  <button type="button" (click)="toggleHide(sec)" class="text-slate-500 hover:underline">{{ sec.isVisible ? 'Hide' : 'Show' }}</button>
                  <button type="button" (click)="duplicate(sec)" class="text-slate-500 hover:underline">Duplicate</button>
                  <button type="button" (click)="remove(sec)" class="text-red-500 hover:underline">Delete</button>
                </div>
              </div>
              @for (f of schema()?.settings ?? []; track f.key) {
                <label class="block mb-3" [attr.data-field-key]="f.key">
                  <span class="lbl">{{ f.label }}</span>
                  <app-section-field [schema]="f" [(value)]="settingsObj[f.key]" [colorSchemes]="schemes" (valueChange)="onFieldEdit()" />
                  @if (f.help) { <span class="text-xs text-slate-400">{{ f.help }}</span> }
                </label>
              }
              @if (!(schema()?.settings ?? []).length) {
                <p class="text-xs text-slate-400 italic">This section has no settings — see its blocks in the list on the left.</p>
              }
            } @else if (focusedBlockSchema(); as bt) {
              <!-- Block-focused panel: only this block's fields, with a breadcrumb back to the section. -->
              <button type="button" (click)="selectSection(sec)" class="text-xs text-slate-500 hover:underline mb-2">← Back to {{ schema()?.label ?? sec.sectionType }}</button>
              <div class="flex items-center justify-between mb-3">
                <h2 class="font-semibold text-slate-800">{{ bt.label }} {{ focusedBlockIndex()! + 1 }}</h2>
                <button type="button" (click)="removeBlock(focusedBlockIndex()!)" class="text-red-500 hover:underline text-xs">Delete</button>
              </div>
              @if (blocksArr[focusedBlockIndex()!]; as b) {
                @for (f of bt.fields; track f.key) {
                  <label class="block mb-3" [attr.data-field-key]="f.key">
                    <span class="lbl">{{ f.label }}</span>
                    <app-section-field [schema]="f" [(value)]="b[f.key]" [colorSchemes]="schemes" (valueChange)="onFieldEdit()" />
                    @if (f.help) { <span class="text-xs text-slate-400">{{ f.help }}</span> }
                  </label>
                }
              }
            }

            <div class="sticky bottom-0 bg-white border-t border-slate-100 -mx-4 px-4 pt-2 pb-1 mt-3">
              @if (dirty()) { <p class="text-[11px] text-amber-600 mb-1">Unsaved changes — shown live in the preview, saved when you click Save</p> }
              <button type="button" (click)="save(sec)" [disabled]="saving()" class="btn-primary w-full">{{ saving() ? 'Saving…' : 'Save section' }}</button>
            </div>
          } @else {
            <div class="text-slate-400 text-sm text-center p-8">Select or add a section to edit it.</div>
          }
        </aside>
        }
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
  private readonly router = inject(Router);

  @ViewChild('previewFrame') previewFrame?: ElementRef<HTMLIFrameElement>;
  @ViewChild('rightPanel') rightPanel?: ElementRef<HTMLElement>;

  readonly templates = signal<ThemeTemplateSummary[]>([]);
  readonly sections = signal<ThemeSectionAdmin[]>([]);
  readonly types = signal<SectionTypeSchema[]>([]);
  readonly activeKey = signal<string>('index');
  readonly selectedId = signal<number | null>(null);
  /** E3: which block of the SELECTED section is focused — drives the right panel view, not just the
   *  canvas outline (T15's old role). null = the section's own settings are shown instead. */
  readonly focusedBlockIndex = signal<number | null>(null);
  /** E3: which specific field to scroll-to/focus once the panel above renders — set by a canvas
   *  field-level click (E3's data-field), consumed once then cleared. */
  private pendingFocusField: string | null = null;
  /** E2: unsaved edits exist for the selected section (already streamed live to the canvas). */
  readonly dirty = signal(false);
  /** E1: set by the canvas "+ Add section" pill — where the next added section should land. */
  readonly pendingInsertBefore = signal<ThemeSectionAdmin | null>(null);
  /** T17: which section's "+ Add block" type-picker menu is open (by section id), for sections
   *  offering more than one block type. */
  readonly addBlockMenuOpen = signal<number | null>(null);
  /** E1: preview device frame. */
  readonly device = signal<'desktop' | 'mobile'>('desktop');
  /** E6: fullscreen preview — collapses both side panels. */
  readonly fullscreen = signal(false);
  /** E6: inspector mode suspends the canvas's editor-mode click interception so a merchant can click
   *  through to real links/Add-to-Cart to sanity-check actual storefront behaviour. */
  readonly inspectorMode = signal(false);
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
  private themeId = 0;
  private previewToken: string | null = null;
  /** Deep link restoration (?template=&section=&block=) applies once, on first sections load. */
  private pendingDeepLink: { section: number | null; block: number | null } | null = null;

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

  // ---------------- E5 remainder: preview context (which product/collection a dynamic template previews with) ----------------
  private sampleProductSlug = '';
  private sampleProductName = 'first product';
  private sampleCategorySlug = '';
  private sampleCategoryName = 'All products';
  readonly previewPickerOpen = signal(false);
  previewQuery = '';
  readonly previewSearch$ = new Subject<string>();
  readonly previewProductResults = signal<{ productId: number; name: string; slug: string }[]>([]);
  readonly previewCategories = signal<{ categoryId: number; name: string; slug: string }[]>([]);
  readonly previewContextLabel = computed(() => this.activeKey() === 'product' ? this.sampleProductName : this.sampleCategoryName);
  togglePreviewPicker(): void {
    if (!this.previewPickerOpen() && this.activeKey() === 'collection' && this.previewCategories().length === 0) {
      this.catalog.getCategories().subscribe((c) => this.previewCategories.set(c));
    }
    this.previewPickerOpen.update((v) => !v);
  }
  pickPreviewProduct(p: { productId: number; name: string; slug: string }): void {
    this.sampleProductSlug = p.slug; this.sampleProductName = p.name;
    this.previewPickerOpen.set(false); this.previewQuery = ''; this.previewProductResults.set([]);
    this.setPreview();
  }
  pickPreviewCategory(c: { categoryId: number; name: string; slug: string } | null): void {
    this.sampleCategorySlug = c?.slug ?? ''; this.sampleCategoryName = c?.name ?? 'All products';
    this.previewPickerOpen.set(false);
    this.setPreview();
  }

  ngOnInit(): void {
    this.themeId = Number(this.route.snapshot.paramMap.get('themeId'));
    this.catalog.getProducts({ pageSize: 1 }).subscribe((r) => {
      if (r.items[0]) { this.sampleProductSlug = r.items[0].slug; this.sampleProductName = r.items[0].name; }
    });
    this.previewSearch$.pipe(
      debounceTime(250), distinctUntilChanged(),
      switchMap((q) => this.catalog.getProducts({ search: q.trim(), pageSize: 8 })),
    ).subscribe((r) => this.previewProductResults.set(r.items));

    this.library.get(this.themeId).subscribe((t) => { this.themeName.set(t.name); this.previewToken = t.previewToken; this.setPreview(); });
    this.svc.templates(this.themeId).subscribe((t) => this.templates.set(t));
    this.svc.getSettings(this.themeId).subscribe((s) => {
      this.themeSettings = { ...s };
      this.schemes = this.parse(s['ColorSchemes'] ?? null, [] as ColorScheme[]);
    });

    const q = this.route.snapshot.queryParamMap;
    const deepTemplate = q.get('template');
    const deepSection = q.get('section') ? Number(q.get('section')) : null;
    const deepBlock = q.get('block') ? Number(q.get('block')) : null;
    if (deepSection !== null) this.pendingDeepLink = { section: deepSection, block: deepBlock };
    this.selectTemplate(deepTemplate || 'index');

    if (typeof window !== 'undefined') {
      window.addEventListener('message', this.onWindowMessage);
      window.addEventListener('keydown', this.onKeydown);
    }
  }

  ngOnDestroy(): void {
    if (this.draftTimer) clearTimeout(this.draftTimer);
    if (typeof window === 'undefined') return;
    window.removeEventListener('message', this.onWindowMessage);
    window.removeEventListener('keydown', this.onKeydown);
  }

  /** T15/E1/E3: a merchant clicked a section/block/field, a toolbar action, or an insert pill in the preview. */
  private readonly onWindowMessage = (event: MessageEvent): void => {
    if (typeof window === 'undefined' || event.origin !== window.location.origin) return;
    const data = event.data;
    if (data?.type === 'theme-editor:select') {
      const sec = this.sections().find((x) => x.id === data.sectionId);
      if (!sec) return;
      const field = typeof data.field === 'string' ? data.field : null;
      if (typeof data.blockIndex === 'number') {
        if (!this.selectBlockOf(sec, data.blockIndex, field)) return;
      } else {
        if (!this.selectSection(sec, field)) return;
      }
      if (typeof data.blockIndex === 'number') {
        queueMicrotask(() => document.querySelector(`[data-tree-block="${data.blockIndex}"]`)?.scrollIntoView({ block: 'nearest' }));
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
      if (this.selectedId() !== null) this.postHighlight(this.selectedId(), this.focusedBlockIndex());
    }
  };

  /** E6: Esc deselects/closes open panels; Ctrl+Z/Ctrl+Y undo/redo; Up/Down reorders the focused row. */
  private readonly onKeydown = (event: KeyboardEvent): void => {
    const tag = (event.target as HTMLElement)?.tagName;
    const typing = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
    if (event.key === 'Escape') {
      if (this.navigatorOpen()) { this.navigatorOpen.set(false); return; }
      if (this.previewPickerOpen()) { this.previewPickerOpen.set(false); return; }
      if (this.focusedBlockIndex() !== null) { const sec = this.selected(); if (sec) this.selectSection(sec); return; }
      if (this.selectedId() !== null) { this.selectedId.set(null); this.postHighlight(null); }
    } else if ((event.ctrlKey || event.metaKey) && !typing && event.key.toLowerCase() === 'z') {
      event.preventDefault(); this.undo();
    } else if ((event.ctrlKey || event.metaKey) && !typing && event.key.toLowerCase() === 'y') {
      event.preventDefault(); this.redo();
    } else if (!typing && (event.key === 'ArrowUp' || event.key === 'ArrowDown')) {
      const dir = event.key === 'ArrowUp' ? -1 : 1;
      if (this.focusedBlockIndex() !== null) this.reorderFocusedBlock(dir);
      else if (this.selectedId() !== null) this.reorderFocusedSection(dir);
    }
  };

  private reorderFocusedSection(dir: -1 | 1): void {
    const arr = [...this.sections()];
    const i = arr.findIndex((s) => s.id === this.selectedId());
    const j = i + dir;
    if (i < 0 || j < 0 || j >= arr.length) return;
    moveItemInArray(arr, i, j);
    this.sections.set(arr);
    this.svc.reorder(this.themeId, this.activeKey(), arr.map((s) => s.id)).subscribe(() => this.reloadPreview());
  }
  private reorderFocusedBlock(dir: -1 | 1): void {
    const i = this.focusedBlockIndex()!;
    const j = i + dir;
    if (j < 0 || j >= this.blocksArr.length) return;
    moveItemInArray(this.blocksArr, i, j);
    this.focusedBlockIndex.set(j);
    this.onFieldEdit();
  }

  private postToPreview(message: unknown): void {
    this.previewFrame?.nativeElement.contentWindow?.postMessage(message, window.location.origin);
  }

  /** Mirror the current selection into the preview so it outlines the section (and block/field, when set). */
  private postHighlight(sectionId: number | null, blockIndex: number | null = null, field: string | null = null): void {
    this.postToPreview({ type: 'theme-editor:highlight', sectionId, blockIndex, field });
  }

  /** E2: any field edit → mark dirty + debounce-stream the draft into the canvas (live preview). */
  onFieldEdit(): void {
    this.dirty.set(true);
    this.pushUndoSnapshot();
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
    this.undoStack = []; this.redoStack = [];
    return true;
  }

  toggleSettings(): void { this.settingsMode.update((v) => !v); }
  toggleFullscreen(): void { this.fullscreen.update((v) => !v); }
  toggleInspector(): void {
    this.inspectorMode.update((v) => !v);
    this.postToPreview({ type: 'theme-editor:inspector', enabled: this.inspectorMode() });
  }

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
    this.focusedBlockIndex.set(null);
    this.svc.sectionTypes(key).subscribe((t) => this.types.set(t));
    this.loadSections();
    this.setPreview();
  }

  private loadSections(): void {
    this.svc.sections(this.themeId, this.activeKey()).subscribe((s) => {
      this.sections.set(s);
      if (this.selectedId() && !s.some((x) => x.id === this.selectedId())) this.selectedId.set(null);
      this.svc.templates(this.themeId).subscribe((t) => this.templates.set(t));   // refresh section counts

      const deep = this.pendingDeepLink;
      if (deep && s.some((x) => x.id === deep.section)) {
        this.pendingDeepLink = null;
        const sec = s.find((x) => x.id === deep.section)!;
        if (deep.block !== null) this.selectBlockOf(sec, deep.block);
        else this.selectSection(sec);
      }
    });
  }

  selected(): ThemeSectionAdmin | null { return this.sections().find((s) => s.id === this.selectedId()) ?? null; }
  schema(): SectionTypeSchema | null { const s = this.selected(); return s ? this.types().find((t) => t.key === s.sectionType) ?? null : null; }
  blockType(): BlockTypeSchema | null { return this.schema()?.blockTypes?.[0] ?? null; }
  /** T17: every block type the selected section's schema allows — length 1 for every existing
   *  section (Hero's Slides, Multicolumn's Columns, …), length >1 only for CustomSection's mixed
   *  Heading/Text/Image/Button/Spacer/Divider palette. */
  blockTypes(): BlockTypeSchema[] { return this.schema()?.blockTypes ?? []; }
  /** T17: which schema applies to the currently-focused block. Single-type sections: same as
   *  blockType(). Multi-type sections: resolved from the block's own stamped `type`. */
  focusedBlockSchema(): BlockTypeSchema | null {
    const i = this.focusedBlockIndex();
    if (i === null) return null;
    const types = this.blockTypes();
    if (types.length <= 1) return types[0] ?? null;
    return types.find((t) => t.key === this.blocksArr[i]?.['type']) ?? null;
  }

  /** E3: dynamic block title — first non-empty of the common "this is what it says" keys, else a
   *  numbered fallback. Covers every block schema in this codebase (heading/title/text/question/
   *  label/value) without needing a per-section-type mapping. */
  blockTitle(b: Record<string, any>, index: number): string {
    const label = b['heading'] ?? b['title'] ?? b['text'] ?? b['question'] ?? b['label'] ?? b['value'];
    const bt = this.blockType();
    return (typeof label === 'string' && label.trim()) ? label.trim() : `${bt?.label ?? 'Block'} ${index + 1}`;
  }

  /** Section row click → focus the section's own settings (collapses any block focus). Returns
   *  false if a dirty draft was kept (selection did not change). */
  selectSection(s: ThemeSectionAdmin, focusFieldKey: string | null = null): boolean {
    const changingSection = s.id !== this.selectedId();
    if (changingSection && !this.confirmDiscard()) return false;
    if (changingSection) {
      this.selectedId.set(s.id);
      this.settingsObj = this.parse(s.settings, {});
      this.blocksArr = this.parse(s.blocks, []);
      this.undoStack = []; this.redoStack = [];
    }
    this.focusedBlockIndex.set(null);
    this.postHighlight(s.id, null, focusFieldKey);
    this.syncUrl();
    if (focusFieldKey) this.pendingFocusField = focusFieldKey, queueMicrotask(() => this.applyPendingFocus());
    return true;
  }

  /** Block row/canvas click → focus one block's fields. */
  selectBlock(index: number): void {
    const s = this.selected();
    if (!s) return;
    this.selectBlockOf(s, index);
  }
  private selectBlockOf(s: ThemeSectionAdmin, index: number, focusFieldKey: string | null = null): boolean {
    const changingSection = s.id !== this.selectedId();
    if (changingSection && !this.confirmDiscard()) return false;
    if (changingSection) {
      this.selectedId.set(s.id);
      this.settingsObj = this.parse(s.settings, {});
      this.blocksArr = this.parse(s.blocks, []);
      this.undoStack = []; this.redoStack = [];
    }
    if (index < 0 || index >= this.blocksArr.length) return true;
    this.focusedBlockIndex.set(index);
    this.postHighlight(s.id, index, focusFieldKey);
    this.syncUrl();
    if (focusFieldKey) this.pendingFocusField = focusFieldKey, queueMicrotask(() => this.applyPendingFocus());
    return true;
  }

  /** E3: scroll to + focus the input for the field a canvas click landed on. */
  private applyPendingFocus(): void {
    const key = this.pendingFocusField;
    this.pendingFocusField = null;
    if (!key) return;
    queueMicrotask(() => {
      const root = this.rightPanel?.nativeElement;
      const wrapper = root?.querySelector(`[data-field-key="${key}"]`);
      wrapper?.scrollIntoView({ block: 'center' });
      wrapper?.querySelector<HTMLElement>('input, textarea, select, button')?.focus();
    });
  }

  /** E3: deep-linkable editor state — mirrors CollectionPageStore.applyFilters' query-merge pattern. */
  private syncUrl(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { template: this.activeKey(), section: this.selectedId(), block: this.focusedBlockIndex() },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  addSection(type: string): void {
    if (!type) return;
    this.svc.addSection(this.themeId, this.activeKey(), type).subscribe((sec) => {
      const before = this.pendingInsertBefore();
      this.pendingInsertBefore.set(null);
      // select() runs before the reload lands — harmless; the 'theme-editor:ready' handshake
      // (fired when the reloaded iframe's new document mounts) re-asserts the highlight reliably.
      const finish = () => { this.loadSections(); this.selectSection(sec); this.reloadPreview(); };
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

  /** E3: reorder blocks within the selected section — no dedicated backend endpoint; blocks are one
   *  ordered JSON array, so persisting a reorder is the same save() path any other block edit uses. */
  dropBlock(e: CdkDragDrop<Record<string, any>[]>): void {
    moveItemInArray(this.blocksArr, e.previousIndex, e.currentIndex);
    if (this.focusedBlockIndex() === e.previousIndex) this.focusedBlockIndex.set(e.currentIndex);
    this.onFieldEdit();
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
    // T17: stamp which primitive this block is when a section offers more than one kind (e.g.
    // CustomSection) — schemaOfBlock() below needs it to resolve the right fields per block.
    // Harmless no-op for every existing single-block-type section.
    if (this.blockTypes().length > 1) b['type'] = bt.key;
    this.blocksArr = [...this.blocksArr, b];
    this.focusedBlockIndex.set(this.blocksArr.length - 1);
    this.onFieldEdit();
  }
  removeBlock(i: number): void {
    this.blocksArr = this.blocksArr.filter((_, idx) => idx !== i);
    this.focusedBlockIndex.set(null);
    this.onFieldEdit();
  }

  // ---------------- E6: session-scoped undo/redo over {settingsObj, blocksArr} ----------------
  private undoStack: { settings: string; blocks: string }[] = [];
  private redoStack: { settings: string; blocks: string }[] = [];
  readonly canUndo = signal(false);
  readonly canRedo = signal(false);
  private pushUndoSnapshot(): void {
    this.undoStack.push({ settings: JSON.stringify(this.settingsObj), blocks: JSON.stringify(this.blocksArr) });
    if (this.undoStack.length > 50) this.undoStack.shift();
    this.redoStack = [];
    this.canUndo.set(true); this.canRedo.set(false);
  }
  undo(): void {
    if (!this.undoStack.length) return;
    this.redoStack.push({ settings: JSON.stringify(this.settingsObj), blocks: JSON.stringify(this.blocksArr) });
    const prev = this.undoStack.pop()!;
    this.settingsObj = this.parse(prev.settings, {});
    this.blocksArr = this.parse(prev.blocks, []);
    this.dirty.set(true);
    this.postDraft();
    this.canUndo.set(this.undoStack.length > 0); this.canRedo.set(true);
  }
  redo(): void {
    if (!this.redoStack.length) return;
    this.undoStack.push({ settings: JSON.stringify(this.settingsObj), blocks: JSON.stringify(this.blocksArr) });
    const next = this.redoStack.pop()!;
    this.settingsObj = this.parse(next.settings, {});
    this.blocksArr = this.parse(next.blocks, []);
    this.dirty.set(true);
    this.postDraft();
    this.canRedo.set(this.redoStack.length > 0); this.canUndo.set(true);
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
      case 'collection': return this.sampleCategorySlug ? `/category/${this.sampleCategorySlug}` : '/products';
      case 'list-collections': return '/collections';
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
