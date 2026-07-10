import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { ThemeLibraryService, ThemeSummary } from '../../../core/services/theme-library.service';

/**
 * Theme library (S5): the tenant's themes — exactly one Published (live), the rest Draft.
 * Create / duplicate / rename / publish / delete, edit in the theme editor, and preview a
 * Draft on the storefront via its preview token before making it live.
 */
@Component({
  selector: 'app-admin-theme-library',
  imports: [RouterLink, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Themes</h1>
        <button type="button" (click)="create()" [disabled]="busy()" class="btn-primary px-4 py-2 text-sm">+ New theme</button>
      </div>
      <p class="text-sm text-slate-500 mb-5">Your store shows the <span class="font-medium">Published</span> theme. Edit a draft, preview it, then publish to go live.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else {
        <div class="grid sm:grid-cols-2 gap-4">
          @for (t of themes(); track t.themeId) {
            <div class="bg-white border rounded-xl p-5" [class]="t.isPublished ? 'border-green-300 ring-1 ring-green-100' : 'border-slate-200'">
              <div class="flex items-start justify-between gap-2">
                <div>
                  <div class="flex items-center gap-2">
                    <h2 class="font-semibold text-slate-800">{{ t.name }}</h2>
                    @if (t.isPublished) { <span class="text-xs px-2 py-0.5 rounded-full bg-green-50 text-green-700">Published</span> }
                    @else { <span class="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-500">Draft</span> }
                  </div>
                  <p class="text-xs text-slate-400 mt-1">Created {{ t.createdAt | date:'d MMM y' }}</p>
                </div>
              </div>

              <div class="flex flex-wrap gap-2 mt-4 text-sm">
                <a [routerLink]="['/admin/theme-editor', t.themeId]" class="px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50">Edit</a>
                @if (t.previewToken) {
                  <a [href]="previewUrl(t)" target="_blank" rel="noopener" class="px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50">Preview ↗</a>
                }
                @if (!t.isPublished) {
                  <button type="button" (click)="publish(t)" [disabled]="busy()" class="px-3 py-1.5 rounded-lg bg-primary text-white hover:bg-primary-dark">Publish</button>
                }
                <button type="button" (click)="duplicate(t)" [disabled]="busy()" class="px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50">Duplicate</button>
                <button type="button" (click)="rename(t)" [disabled]="busy()" class="px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50">Rename</button>
                @if (!t.isPublished) {
                  <button type="button" (click)="remove(t)" [disabled]="busy()" class="px-3 py-1.5 rounded-lg border border-red-200 text-red-600 hover:bg-red-50">Delete</button>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminThemeLibraryComponent implements OnInit {
  private readonly api = inject(ThemeLibraryService);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly themes = signal<ThemeSummary[]>([]);

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.list().subscribe({ next: (t) => { this.themes.set(t); this.loading.set(false); }, error: () => this.loading.set(false) }); }
  private flash(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 2500); }

  /** Storefront URL that renders this (draft) theme via its preview token. */
  previewUrl(t: ThemeSummary): string { return `${SITE_URL}/?preview=${t.previewToken}`; }

  create(): void {
    const name = typeof window !== 'undefined' ? window.prompt('Name your new theme', 'New theme') : 'New theme';
    if (!name) return;
    this.busy.set(true);
    this.api.create(name).subscribe({ next: () => { this.busy.set(false); this.flash('Theme created.'); this.load(); }, error: () => this.busy.set(false) });
  }
  duplicate(t: ThemeSummary): void {
    this.busy.set(true);
    this.api.duplicate(t.themeId, `${t.name} copy`).subscribe({ next: () => { this.busy.set(false); this.flash('Theme duplicated.'); this.load(); }, error: () => this.busy.set(false) });
  }
  rename(t: ThemeSummary): void {
    const name = typeof window !== 'undefined' ? window.prompt('Rename theme', t.name) : null;
    if (!name || name === t.name) return;
    this.busy.set(true);
    this.api.rename(t.themeId, name).subscribe({ next: () => { this.busy.set(false); this.flash('Renamed.'); this.load(); }, error: () => this.busy.set(false) });
  }
  publish(t: ThemeSummary): void {
    if (typeof window !== 'undefined' && !window.confirm(`Publish "${t.name}"? It becomes your live store theme.`)) return;
    this.busy.set(true);
    this.api.publish(t.themeId).subscribe({ next: () => { this.busy.set(false); this.flash(`"${t.name}" is now live.`); this.load(); }, error: () => this.busy.set(false) });
  }
  remove(t: ThemeSummary): void {
    if (typeof window !== 'undefined' && !window.confirm(`Delete "${t.name}"? This can't be undone.`)) return;
    this.busy.set(true);
    this.api.remove(t.themeId).subscribe({ next: () => { this.busy.set(false); this.flash('Theme deleted.'); this.load(); }, error: () => this.busy.set(false) });
  }
}
