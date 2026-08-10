import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { ContentPage, ContentSection } from '../../../core/models/content-page.model';
import { RichTextComponent } from '../../../shared/rich-text/rich-text.component';

interface Draft {
  sectionId: number | null;
  sectionType: string;
  title: string;
  content: string;
  isVisible: boolean;
}

/**
 * Editing the content pages — About, FAQ, Buying guide, Contact.
 *
 * Sections are typed, so the form changes shape with the type: Prose and Faq get the rich-text
 * box, while Stats, Cards and Cta are small repeating lists. About is an intro, a grid of
 * numbers and a row of cards, and one big HTML field would have made it "editable" by
 * flattening it.
 */
@Component({
  selector: 'app-admin-pages',
  imports: [FormsModule, RichTextComponent],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Pages</h1>
      <p class="text-sm text-slate-500 mb-5">The wording on your About, FAQ, Buying guide and Contact pages.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="flex gap-1 p-1 mb-5 bg-slate-100 rounded-lg text-sm font-medium w-fit">
        @for (p of pages(); track p.slug) {
          <button type="button" (click)="select(p.slug)" class="px-4 py-2 rounded-md transition"
                  [class]="slug() === p.slug ? 'bg-white shadow-sm text-slate-900' : 'text-slate-500'">{{ p.title }}</button>
        }
      </div>

      @if (loading()) { <div class="p-10 text-center text-slate-400">Loading…</div> }
      @else if (page(); as pg) {

        <!-- Page settings -->
        <div class="bg-white border border-slate-200 rounded-xl p-4 mb-4">
          <div class="grid gap-3 sm:grid-cols-2">
            <label class="text-sm">
              <span class="block text-slate-600 mb-1">Page heading</span>
              <input [(ngModel)]="pageForm.title" class="input w-full" />
            </label>
            <label class="text-sm flex items-end gap-2 pb-2">
              <input type="checkbox" [(ngModel)]="pageForm.isPublished" class="w-4 h-4" />
              <span class="text-slate-600">Published — visible on the site</span>
            </label>
            <label class="text-sm">
              <span class="block text-slate-600 mb-1">Search title</span>
              <input [(ngModel)]="pageForm.metaTitle" class="input w-full" placeholder="Shown as the tab and search result title" />
            </label>
            <label class="text-sm">
              <span class="block text-slate-600 mb-1">Search description</span>
              <input [(ngModel)]="pageForm.metaDescription" class="input w-full" placeholder="The sentence under the search result" />
            </label>
          </div>
          <button type="button" (click)="savePage()" [disabled]="saving()"
                  class="mt-3 px-4 py-2 rounded-lg bg-slate-900 text-white text-sm font-medium disabled:opacity-50">Save page</button>
        </div>

        <!-- Sections -->
        <div class="space-y-3">
          @for (s of pg.sections; track s.sectionId; let i = $index) {
            <div class="bg-white border border-slate-200 rounded-xl p-4" [class.opacity-60]="!s.isVisible">
              <div class="flex items-center gap-2 mb-2">
                <span class="text-[11px] uppercase tracking-wide text-slate-400">{{ label(s.sectionType) }}</span>
                @if (!s.isVisible) { <span class="text-[10px] text-amber-700 bg-amber-50 border border-amber-200 rounded px-1">hidden</span> }
                <span class="ml-auto flex items-center gap-1.5 text-xs">
                  <button type="button" (click)="move(i, -1)" [disabled]="i === 0" class="text-blue-600 hover:underline disabled:text-slate-300">↑</button>
                  <button type="button" (click)="move(i, 1)" [disabled]="i === pg.sections.length - 1" class="text-blue-600 hover:underline disabled:text-slate-300">↓</button>
                  <span class="text-slate-300">·</span>
                  <button type="button" (click)="edit(s)" class="text-blue-600 hover:underline">Edit</button>
                  <span class="text-slate-300">·</span>
                  <button type="button" (click)="remove(s)" class="text-red-600 hover:underline">Delete</button>
                </span>
              </div>
              @if (s.title) { <div class="font-medium text-slate-800">{{ s.title }}</div> }
              <div class="text-sm text-slate-500 mt-1 line-clamp-3">{{ preview(s) }}</div>
            </div>
          }
        </div>

        <div class="flex flex-wrap gap-2 mt-4">
          @for (t of typesFor(pg.slug); track t.type) {
            <button type="button" (click)="add(t.type)"
                    class="px-3 py-1.5 rounded-lg border border-slate-300 text-sm hover:bg-slate-50">+ {{ t.label }}</button>
          }
        </div>

        <!-- Editor -->
        @if (draft(); as d) {
          <div class="fixed inset-0 bg-black/30 flex items-start justify-center p-6 overflow-y-auto z-50" (click)="draft.set(null)">
            <div class="bg-white rounded-xl border border-slate-200 p-5 w-full max-w-2xl mt-8 mb-8" (click)="$event.stopPropagation()">
              <h2 class="font-semibold text-slate-900 mb-3">{{ d.sectionId ? 'Edit' : 'Add' }} {{ label(d.sectionType).toLowerCase() }}</h2>

              @if (d.sectionType !== 'Stats' && d.sectionType !== 'Cta') {
                <label class="text-sm block mb-3">
                  <span class="block text-slate-600 mb-1">{{ d.sectionType === 'Faq' ? 'Question' : 'Heading' }}</span>
                  <input [(ngModel)]="d.title" class="input w-full" />
                </label>
              }

              @if (d.sectionType === 'Prose' || d.sectionType === 'Faq') {
                <span class="block text-sm text-slate-600 mb-1">{{ d.sectionType === 'Faq' ? 'Answer' : 'Text' }}</span>
                <app-rich-text [(ngModel)]="d.content" [ngModelOptions]="{ standalone: true }" />
              } @else {
                <span class="block text-sm text-slate-600 mb-1">Items</span>
                <textarea [(ngModel)]="d.content" rows="8" class="input w-full font-mono text-xs"></textarea>
                <p class="text-[11px] text-slate-400 mt-1">{{ jsonHint(d.sectionType) }}</p>
              }

              <label class="flex items-center gap-2 text-sm text-slate-600 mt-3">
                <input type="checkbox" [(ngModel)]="d.isVisible" class="w-4 h-4" /> Show this on the page
              </label>

              @if (draftError()) { <p class="text-sm text-red-600 mt-2">{{ draftError() }}</p> }

              <div class="flex items-center gap-2 mt-4">
                <button type="button" (click)="saveDraft()" [disabled]="saving()"
                        class="px-4 py-2 rounded-lg bg-slate-900 text-white text-sm font-medium disabled:opacity-50">
                  {{ saving() ? 'Saving…' : 'Save' }}
                </button>
                <button type="button" (click)="draft.set(null)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm">Cancel</button>
              </div>
            </div>
          </div>
        }
      }
    </div>
  `,
})
export class AdminPagesComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/cms/pages`;

  readonly pages = signal<ContentPage[]>([]);
  readonly page = signal<ContentPage | null>(null);
  readonly slug = signal('');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly draft = signal<Draft | null>(null);
  readonly draftError = signal<string | null>(null);

  pageForm = { title: '', isPublished: true, metaTitle: '', metaDescription: '' };

  ngOnInit(): void {
    this.http.get<ApiResponse<ContentPage[]>>(this.base).subscribe({
      next: (r) => {
        const list = r.data ?? [];
        this.pages.set(list);
        if (list.length) this.select(list[0].slug); else this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.error.set('Could not load pages.'); },
    });
  }

  select(slug: string): void {
    this.slug.set(slug);
    this.loading.set(true);
    this.error.set(null);
    this.http.get<ApiResponse<ContentPage>>(`${this.base}/${slug}`).subscribe({
      next: (r) => { this.apply(r.data ?? null); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Could not load that page.'); },
    });
  }

  private apply(p: ContentPage | null): void {
    this.page.set(p);
    if (p) this.pageForm = {
      title: p.title, isPublished: p.isPublished,
      metaTitle: p.metaTitle ?? '', metaDescription: p.metaDescription ?? '',
    };
  }

  /** Which section types make sense on which page — FAQ takes questions, the rest take prose. */
  typesFor(slug: string): { type: string; label: string }[] {
    if (slug === 'faq') return [{ type: 'Faq', label: 'Question' }];
    if (slug === 'about') return [
      { type: 'Prose', label: 'Text' }, { type: 'Stats', label: 'Numbers' },
      { type: 'Cards', label: 'Cards' }, { type: 'Cta', label: 'Call to action' },
    ];
    return [{ type: 'Prose', label: 'Text' }];
  }

  label(type: string): string {
    return { Prose: 'Text', Faq: 'Question', Stats: 'Numbers', Cards: 'Cards', Cta: 'Call to action' }[type] ?? type;
  }

  jsonHint(type: string): string {
    if (type === 'Stats') return 'One entry per statistic: [{"value":"10,000+","label":"Calendars printed"}]';
    if (type === 'Cards') return 'One entry per card: [{"icon":"🖨️","title":"Heading","text":"Sentence"}]';
    return '{"heading":"…","text":"…","buttonLabel":"Shop","buttonLink":"/order"}';
  }

  preview(s: ContentSection): string {
    return (s.content ?? '').replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 200) || '(empty)';
  }

  // --- editing ---

  add(type: string): void {
    this.draftError.set(null);
    const blank = type === 'Stats' ? '[]' : type === 'Cards' ? '[]'
      : type === 'Cta' ? '{"heading":"","text":"","buttonLabel":"","buttonLink":"/order"}' : '';
    this.draft.set({ sectionId: null, sectionType: type, title: '', content: blank, isVisible: true });
  }

  edit(s: ContentSection): void {
    this.draftError.set(null);
    this.draft.set({
      sectionId: s.sectionId, sectionType: s.sectionType,
      title: s.title ?? '', content: s.content ?? '', isVisible: s.isVisible,
    });
  }

  saveDraft(): void {
    const d = this.draft();
    if (!d) return;

    // Checked here as well as on the server: a JSON mistake should be caught before the round
    // trip, while the box with the mistake in it is still on screen.
    if (d.sectionType !== 'Prose' && d.sectionType !== 'Faq') {
      try { JSON.parse(d.content || '[]'); }
      catch { this.draftError.set('That is not valid JSON — check the brackets and quotes.'); return; }
    }

    this.saving.set(true);
    this.draftError.set(null);
    const body = { sectionType: d.sectionType, title: d.title, content: d.content, isVisible: d.isVisible };
    const req = d.sectionId
      ? this.http.put<ApiResponse<ContentPage>>(`${this.base}/sections/${d.sectionId}`, body)
      : this.http.post<ApiResponse<ContentPage>>(`${this.base}/${this.slug()}/sections`, body);

    req.subscribe({
      next: (r) => { this.saving.set(false); this.draft.set(null); this.apply(r.data ?? null); this.flash('Saved.'); },
      error: (e) => { this.saving.set(false); this.draftError.set(e?.error?.message ?? 'Could not save.'); },
    });
  }

  remove(s: ContentSection): void {
    if (!confirm('Delete this section? This cannot be undone.')) return;
    this.http.delete<ApiResponse<ContentPage>>(`${this.base}/sections/${s.sectionId}`).subscribe({
      next: (r) => { this.apply(r.data ?? null); this.flash('Deleted.'); },
      error: (e) => this.error.set(e?.error?.message ?? 'Could not delete.'),
    });
  }

  move(index: number, by: number): void {
    const p = this.page();
    if (!p) return;
    const ids = p.sections.map((s) => s.sectionId);
    const to = index + by;
    if (to < 0 || to >= ids.length) return;
    [ids[index], ids[to]] = [ids[to], ids[index]];

    this.http.put<ApiResponse<ContentPage>>(`${this.base}/${this.slug()}/order`, { sectionIds: ids }).subscribe({
      next: (r) => this.apply(r.data ?? null),
      error: (e) => this.error.set(e?.error?.message ?? 'Could not reorder.'),
    });
  }

  savePage(): void {
    this.saving.set(true);
    this.error.set(null);
    this.http.put<ApiResponse<ContentPage>>(`${this.base}/${this.slug()}`, this.pageForm).subscribe({
      next: (r) => { this.saving.set(false); this.apply(r.data ?? null); this.flash('Saved.'); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Could not save.'); },
    });
  }

  private flash(msg: string): void {
    this.message.set(msg);
    setTimeout(() => this.message.set(null), 2500);
  }
}
