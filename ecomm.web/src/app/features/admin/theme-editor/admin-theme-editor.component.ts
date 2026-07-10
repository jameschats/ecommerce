import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CatalogService } from '../../../core/services/catalog.service';
import {
  BlockTypeSchema, SectionTypeSchema, ThemeAuthoringService, ThemeSectionAdmin, ThemeTemplateSummary,
} from '../../../core/services/theme-authoring.service';

interface TemplateGroup { group: string; templates: ThemeTemplateSummary[]; }

/**
 * Theme editor (S4): pick a page-type template (or a Header/Footer/Announcement zone),
 * add/reorder/configure its sections, and see the storefront preview. Edits the tenant's
 * theme directly (draft/publish arrives in S5). Mirrors the page builder's three-pane UX.
 */
@Component({
  selector: 'app-admin-theme-editor',
  imports: [FormsModule, RouterLink, DragDropModule],
  template: `
    <div class="h-screen flex flex-col">
      <header class="h-12 bg-white border-b border-slate-200 flex items-center justify-between px-4 shrink-0">
        <div class="flex items-center gap-3">
          <a routerLink="/admin" class="text-slate-500 hover:text-slate-800 text-sm">← Admin</a>
          <span class="font-semibold text-slate-800">Theme editor</span>
          <select [ngModel]="activeKey()" (ngModelChange)="selectTemplate($event)" class="input text-sm py-1">
            @for (g of grouped(); track g.group) {
              <optgroup [label]="g.group">
                @for (t of g.templates; track t.templateKey) {
                  <option [value]="t.templateKey">{{ t.label }}{{ t.sectionCount ? ' (' + t.sectionCount + ')' : '' }}</option>
                }
              </optgroup>
            }
          </select>
        </div>
        <div class="flex items-center gap-2">
          @if (message()) { <span class="text-xs text-green-600">{{ message() }}</span> }
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
          </div>
        </aside>

        <!-- center: live preview -->
        <main class="flex-1 bg-slate-100 min-w-0">
          @if (previewUrl()) { <iframe [src]="previewUrl()" class="w-full h-full border-0" title="preview"></iframe> }
        </main>

        <!-- right: settings for selected section -->
        <aside class="w-80 bg-white border-l border-slate-200 overflow-auto p-4 shrink-0">
          @if (selected(); as sec) {
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
                @switch (f.type) {
                  @case ('textarea') { <textarea [(ngModel)]="settingsObj[f.key]" rows="3" class="input w-full"></textarea> }
                  @case ('richtext') { <textarea [(ngModel)]="settingsObj[f.key]" rows="5" class="input w-full font-mono text-xs" placeholder="<p>HTML — scripts are stripped</p>"></textarea> }
                  @case ('boolean') { <input type="checkbox" [(ngModel)]="settingsObj[f.key]" /> }
                  @case ('number') { <input type="number" [(ngModel)]="settingsObj[f.key]" class="input w-full" /> }
                  @case ('color') { <input type="color" [(ngModel)]="settingsObj[f.key]" class="input h-9 w-16" /> }
                  @case ('select') { <select [(ngModel)]="settingsObj[f.key]" class="input w-full">@for (o of f.options ?? []; track o) { <option [value]="o">{{ o }}</option> }</select> }
                  @default { <input [(ngModel)]="settingsObj[f.key]" class="input w-full" /> }
                }
                @if (f.help) { <span class="text-xs text-slate-400">{{ f.help }}</span> }
              </label>
            }

            @if (blockType(); as bt) {
              <div class="border-t border-slate-100 pt-3 mt-3">
                <div class="flex items-center justify-between mb-2"><span class="lbl mb-0">{{ bt.label }}s</span>
                  <button type="button" (click)="addBlock(bt)" class="text-xs text-blue-600 hover:underline">+ Add</button></div>
                @for (b of blocksArr; track $index) {
                  <div class="border border-slate-200 rounded-lg p-2 mb-2">
                    <div class="flex justify-between text-xs text-slate-400 mb-1"><span>{{ bt.label }} {{ $index + 1 }}</span>
                      <button type="button" (click)="removeBlock($index)" class="text-red-500">×</button></div>
                    @for (f of bt.fields; track f.key) {
                      <label class="block mb-1"><span class="text-xs text-slate-500">{{ f.label }}</span>
                        @if (f.type === 'textarea') { <textarea [(ngModel)]="b[f.key]" rows="2" class="input w-full text-sm"></textarea> }
                        @else if (f.type === 'number') { <input type="number" [(ngModel)]="b[f.key]" class="input w-full text-sm" /> }
                        @else { <input [(ngModel)]="b[f.key]" class="input w-full text-sm" /> }
                      </label>
                    }
                  </div>
                }
              </div>
            }

            <button type="button" (click)="save(sec)" [disabled]="saving()" class="btn-primary w-full mt-3">{{ saving() ? 'Saving…' : 'Save section' }}</button>
          } @else {
            <div class="text-slate-400 text-sm text-center p-8">Select or add a section to edit it.</div>
          }
        </aside>
      </div>
    </div>
  `,
})
export class AdminThemeEditorComponent implements OnInit {
  private readonly svc = inject(ThemeAuthoringService);
  private readonly catalog = inject(CatalogService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly templates = signal<ThemeTemplateSummary[]>([]);
  readonly sections = signal<ThemeSectionAdmin[]>([]);
  readonly types = signal<SectionTypeSchema[]>([]);
  readonly activeKey = signal<string>('index');
  readonly selectedId = signal<number | null>(null);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly previewUrl = signal<SafeResourceUrl | null>(null);

  settingsObj: Record<string, any> = {};
  blocksArr: Record<string, any>[] = [];
  private sampleProductSlug = '';

  readonly grouped = computed<TemplateGroup[]>(() => {
    const order = ['Header', 'Templates', 'Footer'];
    const byGroup = new Map<string, ThemeTemplateSummary[]>();
    for (const t of this.templates()) { const l = byGroup.get(t.group) ?? []; l.push(t); byGroup.set(t.group, l); }
    return order.filter((g) => byGroup.has(g)).map((g) => ({ group: g, templates: byGroup.get(g)! }));
  });
  readonly activeLabel = computed(() => this.templates().find((t) => t.templateKey === this.activeKey())?.label ?? this.activeKey());

  ngOnInit(): void {
    this.catalog.getProducts({ pageSize: 1 }).subscribe((r) => { this.sampleProductSlug = r.items[0]?.slug ?? ''; });
    this.svc.templates().subscribe((t) => this.templates.set(t));
    this.selectTemplate('index');
  }

  selectTemplate(key: string): void {
    this.activeKey.set(key);
    this.selectedId.set(null);
    this.svc.sectionTypes(key).subscribe((t) => this.types.set(t));
    this.loadSections();
    this.setPreview();
  }

  private loadSections(): void {
    this.svc.sections(this.activeKey()).subscribe((s) => {
      this.sections.set(s);
      if (this.selectedId() && !s.some((x) => x.id === this.selectedId())) this.selectedId.set(null);
      this.svc.templates().subscribe((t) => this.templates.set(t));   // refresh section counts
    });
  }

  selected(): ThemeSectionAdmin | null { return this.sections().find((s) => s.id === this.selectedId()) ?? null; }
  schema(): SectionTypeSchema | null { const s = this.selected(); return s ? this.types().find((t) => t.key === s.sectionType) ?? null : null; }
  blockType(): BlockTypeSchema | null { return this.schema()?.blockTypes?.[0] ?? null; }

  select(s: ThemeSectionAdmin): void {
    this.selectedId.set(s.id);
    this.settingsObj = this.parse(s.settings, {});
    this.blocksArr = this.parse(s.blocks, []);
  }

  addSection(type: string): void {
    if (!type) return;
    this.svc.addSection(this.activeKey(), type).subscribe((sec) => { this.loadSections(); setTimeout(() => this.select(sec), 200); this.reloadPreview(); });
  }

  drop(e: CdkDragDrop<ThemeSectionAdmin[]>): void {
    const arr = [...this.sections()];
    moveItemInArray(arr, e.previousIndex, e.currentIndex);
    this.sections.set(arr);
    this.svc.reorder(this.activeKey(), arr.map((s) => s.id)).subscribe(() => this.reloadPreview());
  }

  save(sec: ThemeSectionAdmin): void {
    this.saving.set(true);
    this.svc.updateSection(sec.id, {
      title: sec.title, settings: JSON.stringify(this.settingsObj), blocks: JSON.stringify(this.blocksArr),
      isVisible: sec.isVisible, startsAt: null, endsAt: null,
    }).subscribe(() => { this.saving.set(false); this.toast('Saved.'); this.loadSections(); this.reloadPreview(); });
  }

  toggleHide(sec: ThemeSectionAdmin): void {
    this.svc.updateSection(sec.id, { title: sec.title, settings: sec.settings, blocks: sec.blocks, isVisible: !sec.isVisible, startsAt: null, endsAt: null })
      .subscribe(() => { this.loadSections(); this.reloadPreview(); });
  }
  duplicate(sec: ThemeSectionAdmin): void { this.svc.duplicateSection(sec.id).subscribe(() => this.loadSections()); }
  remove(sec: ThemeSectionAdmin): void {
    if (typeof window !== 'undefined' && !window.confirm('Delete this section?')) return;
    this.svc.deleteSection(sec.id).subscribe(() => { this.selectedId.set(null); this.loadSections(); this.reloadPreview(); });
  }

  addBlock(bt: BlockTypeSchema): void {
    const b: Record<string, any> = {};
    for (const f of bt.fields) b[f.key] = f.default ?? '';
    this.blocksArr = [...this.blocksArr, b];
  }
  removeBlock(i: number): void { this.blocksArr = this.blocksArr.filter((_, idx) => idx !== i); }

  reloadPreview(): void { this.setPreview(true); }
  private setPreview(bust = false): void {
    const path = this.previewPath(this.activeKey());
    const sep = path.includes('?') ? '&' : '?';
    const url = bust ? `${path}${sep}_=${Math.floor(performance.now())}` : path;
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
