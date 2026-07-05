import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import {
  BlockTypeSchema, BuilderPage, BuilderSection, CmsService, PageDetail, SectionTypeSchema,
} from '../../../core/services/cms.service';

@Component({
  selector: 'app-admin-builder',
  imports: [FormsModule, RouterLink, DragDropModule],
  template: `
    <div class="h-screen flex flex-col">
      <header class="h-12 bg-white border-b border-slate-200 flex items-center justify-between px-4 shrink-0">
        <div class="flex items-center gap-3">
          <a routerLink="/admin/pages" class="text-slate-500 hover:text-slate-800 text-sm">← Pages</a>
          <span class="font-semibold text-slate-800">{{ page()?.title }}</span>
          @if (page() && !page()!.isPublished) { <span class="text-xs text-amber-600">draft</span> }
        </div>
        <div class="flex items-center gap-2">
          @if (message()) { <span class="text-xs text-green-600">{{ message() }}</span> }
          <button type="button" (click)="togglePublish()" class="text-sm px-3 py-1 rounded-lg border border-slate-300 hover:bg-slate-50">{{ page()?.isPublished ? 'Unpublish' : 'Publish' }}</button>
          <button type="button" (click)="reloadPreview()" class="text-sm px-3 py-1 rounded-lg border border-slate-300 hover:bg-slate-50">↻ Preview</button>
        </div>
      </header>

      <div class="flex-1 flex min-h-0">
        <!-- left: section list -->
        <aside class="w-64 bg-slate-50 border-r border-slate-200 overflow-auto p-3 shrink-0">
          <div cdkDropList (cdkDropListDropped)="drop($event)" class="space-y-1">
            @for (s of sections(); track s.pageSectionId) {
              <div cdkDrag class="bg-white border rounded-lg px-2 py-2 text-sm cursor-move flex items-center justify-between"
                   [class]="s.pageSectionId === selectedId() ? 'border-blue-500 ring-1 ring-blue-200' : 'border-slate-200'"
                   (click)="select(s)">
                <span class="truncate" [class.text-slate-400]="!s.isVisible">⋮⋮ {{ s.title || s.sectionType }}</span>
                <span class="text-xs text-slate-300">{{ s.sectionType }}</span>
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
          @if (previewUrl()) { <iframe #frame [src]="previewUrl()" class="w-full h-full border-0" title="preview"></iframe> }
        </main>

        <!-- right: settings for selected section -->
        <aside class="w-80 bg-white border-l border-slate-200 overflow-auto p-4 shrink-0">
          @if (selected(); as sec) {
            <div class="flex items-center justify-between mb-3">
              <h2 class="font-semibold text-slate-800">{{ schema()?.label }}</h2>
              <div class="flex gap-2 text-xs">
                <button type="button" (click)="toggleHide(sec)" class="text-slate-500 hover:underline">{{ sec.isVisible ? 'Hide' : 'Show' }}</button>
                <button type="button" (click)="duplicate(sec)" class="text-slate-500 hover:underline">Duplicate</button>
                <button type="button" (click)="remove(sec)" class="text-red-500 hover:underline">Delete</button>
              </div>
            </div>

            <!-- settings -->
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

            <!-- blocks -->
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
export class AdminBuilderComponent implements OnInit {
  private readonly svc = inject(CmsService);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);

  readonly page = signal<BuilderPage | null>(null);
  readonly sections = signal<BuilderSection[]>([]);
  readonly types = signal<SectionTypeSchema[]>([]);
  readonly selectedId = signal<number | null>(null);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly previewUrl = signal<SafeResourceUrl | null>(null);

  settingsObj: Record<string, any> = {};
  blocksArr: Record<string, any>[] = [];
  private pageId = 0;

  ngOnInit(): void {
    this.pageId = Number(this.route.snapshot.paramMap.get('id'));
    this.svc.sectionTypes().subscribe((t) => this.types.set(t));
    this.load();
  }

  private load(): void {
    this.svc.getPageAdmin(this.pageId).subscribe((d: PageDetail) => {
      this.page.set(d.page);
      this.sections.set(d.sections);
      this.setPreview(d.page);
      if (this.selectedId() && !d.sections.some((s) => s.pageSectionId === this.selectedId())) this.selectedId.set(null);
    });
  }

  selected(): BuilderSection | null { return this.sections().find((s) => s.pageSectionId === this.selectedId()) ?? null; }
  schema(): SectionTypeSchema | null { const s = this.selected(); return s ? this.types().find((t) => t.key === s.sectionType) ?? null : null; }
  blockType(): BlockTypeSchema | null { return this.schema()?.blockTypes?.[0] ?? null; }

  select(s: BuilderSection): void {
    this.selectedId.set(s.pageSectionId);
    this.settingsObj = this.parse(s.settings, {});
    this.blocksArr = this.parse(s.blocks, []);
  }

  addSection(type: string): void {
    if (!type) return;
    this.svc.addSection(this.pageId, type).subscribe((sec) => { this.load(); setTimeout(() => this.select(sec), 200); });
  }

  drop(e: CdkDragDrop<BuilderSection[]>): void {
    const arr = [...this.sections()];
    moveItemInArray(arr, e.previousIndex, e.currentIndex);
    this.sections.set(arr);
    this.svc.reorderSections(this.pageId, arr.map((s) => s.pageSectionId)).subscribe(() => this.reloadPreview());
  }

  save(sec: BuilderSection): void {
    this.saving.set(true);
    this.svc.updateSection(sec.pageSectionId, {
      title: sec.title, settings: JSON.stringify(this.settingsObj), blocks: JSON.stringify(this.blocksArr),
      isVisible: sec.isVisible, startsAt: null, endsAt: null,
    }).subscribe(() => { this.saving.set(false); this.toast('Saved.'); this.load(); this.reloadPreview(); });
  }

  toggleHide(sec: BuilderSection): void {
    this.svc.updateSection(sec.pageSectionId, { title: sec.title, settings: sec.settings, blocks: sec.blocks, isVisible: !sec.isVisible, startsAt: null, endsAt: null })
      .subscribe(() => { this.load(); this.reloadPreview(); });
  }
  duplicate(sec: BuilderSection): void { this.svc.duplicateSection(sec.pageSectionId).subscribe(() => this.load()); }
  remove(sec: BuilderSection): void { if (!confirm('Delete this section?')) return; this.svc.deleteSection(sec.pageSectionId).subscribe(() => { this.selectedId.set(null); this.load(); this.reloadPreview(); }); }

  addBlock(bt: BlockTypeSchema): void {
    const b: Record<string, any> = {};
    for (const f of bt.fields) b[f.key] = f.default ?? '';
    this.blocksArr = [...this.blocksArr, b];
  }
  removeBlock(i: number): void { this.blocksArr = this.blocksArr.filter((_, idx) => idx !== i); }

  togglePublish(): void {
    const p = this.page(); if (!p) return;
    this.svc.updatePage(p.pageId, { title: p.title, slug: p.slug, isPublished: !p.isPublished, metaTitle: p.metaTitle, metaDescription: p.metaDescription })
      .subscribe(() => { this.load(); setTimeout(() => this.reloadPreview(), 100); });
  }

  reloadPreview(): void { const p = this.page(); if (p) this.setPreview(p, true); }
  private setPreview(p: BuilderPage, bust = false): void {
    const path = p.type === 'Home' ? '/' : `/pages/${p.slug}`;
    const url = bust ? `${path}?_=${Math.floor(performance.now())}` : path;
    this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
  }

  private parse(json: string | null, fallback: any): any { try { return json ? JSON.parse(json) : fallback; } catch { return fallback; } }
  private toast(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 2500); }
}
