import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BuilderPage, CmsService } from '../../../core/services/cms.service';

@Component({
  selector: 'app-admin-pages',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Pages</h1>
        <button type="button" (click)="creating.set(!creating())" class="btn-primary">+ New page</button>
      </div>
      <p class="text-sm text-slate-500 mb-4">Your storefront pages. Open one to build it with sections (drag to arrange, edit settings, live preview).</p>

      @if (creating()) {
        <div class="bg-white border border-slate-200 rounded-xl p-4 mb-5 grid sm:grid-cols-3 gap-3 items-end">
          <label class="block"><span class="lbl">Title</span><input [(ngModel)]="form.title" class="input w-full" placeholder="About Us" /></label>
          <label class="block"><span class="lbl">Address (slug)</span><input [(ngModel)]="form.slug" class="input w-full" placeholder="about-us" /></label>
          <button type="button" (click)="create()" class="btn-primary">Create</button>
        </div>
      }

      <div class="bg-white border border-slate-200 rounded-xl divide-y divide-slate-100">
        @for (p of pages(); track p.pageId) {
          <div class="flex items-center justify-between px-4 py-3">
            <div>
              <div class="font-medium text-slate-800">{{ p.title }}
                @if (p.type === 'Home') { <span class="text-xs text-blue-600 ml-1">home</span> }
                @if (!p.isPublished) { <span class="text-xs text-amber-600 ml-1">draft</span> }
              </div>
              <div class="text-xs text-slate-400">/{{ p.type === 'Home' ? '' : 'pages/' + p.slug }}</div>
            </div>
            <div class="flex items-center gap-3 text-sm">
              <a [routerLink]="['/admin/pages', p.pageId, 'build']" class="text-blue-600 hover:underline">Build →</a>
              <button type="button" (click)="togglePublish(p)" class="text-slate-500 hover:underline">{{ p.isPublished ? 'Unpublish' : 'Publish' }}</button>
              @if (p.type !== 'Home') { <button type="button" (click)="del(p)" class="text-red-500 hover:underline">Delete</button> }
            </div>
          </div>
        }
        @if (!pages().length) { <div class="p-8 text-center text-slate-400">No pages yet.</div> }
      </div>
    </div>
  `,
})
export class AdminPagesComponent implements OnInit {
  private readonly svc = inject(CmsService);
  readonly pages = signal<BuilderPage[]>([]);
  readonly creating = signal(false);
  form = { title: '', slug: '' };

  ngOnInit(): void { this.load(); }
  private load(): void { this.svc.listPages().subscribe((p) => this.pages.set(p)); }

  create(): void {
    if (!this.form.title.trim()) return;
    this.svc.createPage({ title: this.form.title, slug: this.form.slug, isPublished: false, metaTitle: null, metaDescription: null })
      .subscribe(() => { this.form = { title: '', slug: '' }; this.creating.set(false); this.load(); });
  }
  togglePublish(p: BuilderPage): void {
    this.svc.updatePage(p.pageId, { title: p.title, slug: p.slug, isPublished: !p.isPublished, metaTitle: p.metaTitle, metaDescription: p.metaDescription })
      .subscribe(() => this.load());
  }
  del(p: BuilderPage): void {
    if (p.type === 'Home' || !confirm(`Delete "${p.title}"?`)) return;
    this.svc.deletePage(p.pageId).subscribe(() => this.load());
  }
}
