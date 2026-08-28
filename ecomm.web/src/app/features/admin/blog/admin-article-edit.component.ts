import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Article, BlogService, SaveArticle } from '../../../core/services/blog.service';

@Component({
  selector: 'app-admin-article-edit',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <a routerLink="/admin/articles" class="text-sm text-primary hover:underline">← All articles</a>
      <h1 class="text-xl font-bold text-slate-900 mt-2 mb-4">{{ isNew() ? 'New article' : 'Edit article' }}</h1>

      <!-- AI draft -->
      <div class="bg-violet-50 border border-violet-200 rounded-xl p-4 mb-5">
        <div class="text-sm font-medium text-violet-900">✨ Draft with AI</div>
        <p class="text-xs text-violet-700/70 mb-2">Give a topic and we'll write a first draft in your brand voice. You can edit everything after.</p>
        <div class="flex flex-col sm:flex-row gap-2">
          <input [(ngModel)]="topic" name="topic" class="input flex-1" placeholder="e.g. How to care for cotton sarees" />
          <select [(ngModel)]="draftLang" name="draftLang" class="input sm:w-40">
            <option [ngValue]="null">Brand default</option>
            @for (l of languages; track l) { <option [ngValue]="l">{{ l }}</option> }
          </select>
          <button type="button" (click)="doDraft()" [disabled]="drafting() || !topic.trim()" class="btn-primary disabled:opacity-60 whitespace-nowrap">
            {{ drafting() ? 'Writing…' : 'Draft (12 cr)' }}
          </button>
        </div>
        @if (draftError()) { <p class="text-sm text-red-600 mt-2">{{ draftError() }}</p> }
      </div>

      <div class="space-y-4 bg-white border border-slate-200 rounded-xl p-5">
        <div>
          <label class="lbl">Title</label>
          <input [(ngModel)]="model.title" name="title" class="input" placeholder="Article title" />
        </div>
        <div class="grid sm:grid-cols-2 gap-4">
          <div>
            <label class="lbl">Slug <span class="text-slate-400 font-normal">(optional)</span></label>
            <input [(ngModel)]="model.slug" name="slug" class="input" placeholder="auto from title" />
          </div>
          <div>
            <label class="lbl">Author <span class="text-slate-400 font-normal">(optional)</span></label>
            <input [(ngModel)]="model.authorName" name="author" class="input" />
          </div>
        </div>
        <div>
          <label class="lbl">Excerpt <span class="text-slate-400 font-normal">(shown in listings)</span></label>
          <textarea [(ngModel)]="model.excerpt" name="excerpt" rows="2" class="input"></textarea>
        </div>
        <div>
          <label class="lbl">Cover image URL <span class="text-slate-400 font-normal">(optional)</span></label>
          <input [(ngModel)]="model.coverImageUrl" name="cover" class="input" placeholder="https://…" />
        </div>
        <div>
          <label class="lbl">Body <span class="text-slate-400 font-normal">(HTML — h2, h3, p, ul, li, strong, em)</span></label>
          <textarea [(ngModel)]="model.bodyHtml" name="body" rows="14" class="input font-mono text-sm"></textarea>
        </div>
        <details class="text-sm">
          <summary class="cursor-pointer text-slate-600">SEO meta</summary>
          <div class="mt-3 space-y-3">
            <div>
              <label class="lbl">Meta title</label>
              <input [(ngModel)]="model.metaTitle" name="metaTitle" class="input" />
            </div>
            <div>
              <label class="lbl">Meta description</label>
              <textarea [(ngModel)]="model.metaDescription" name="metaDesc" rows="2" class="input"></textarea>
            </div>
          </div>
        </details>

        @if (error()) { <p class="text-sm text-red-600">{{ error() }}</p> }
        <div class="flex flex-wrap items-center gap-2 pt-1">
          <button type="button" (click)="save(false)" [disabled]="saving() || !model.title.trim()" class="btn-primary disabled:opacity-60">
            {{ saving() ? 'Saving…' : 'Save draft' }}
          </button>
          <button type="button" (click)="save(true)" [disabled]="saving() || !model.title.trim()" class="btn-ghost border border-emerald-300 text-emerald-700">
            Save &amp; publish
          </button>
          @if (savedMsg()) { <span class="text-sm text-emerald-700">{{ savedMsg() }}</span> }
        </div>
      </div>
    </div>
  `,
})
export class AdminArticleEditComponent implements OnInit {
  private readonly api = inject(BlogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly id = signal<number | null>(null);
  readonly isNew = signal(true);
  readonly saving = signal(false);
  readonly drafting = signal(false);
  readonly error = signal<string | null>(null);
  readonly draftError = signal<string | null>(null);
  readonly savedMsg = signal<string | null>(null);

  readonly languages = ['English', 'Hindi', 'Tamil', 'Telugu', 'Hinglish'];
  topic = '';
  draftLang: string | null = null;

  model: SaveArticle = { title: '', slug: '', excerpt: '', bodyHtml: '', coverImageUrl: '', authorName: '', metaTitle: '', metaDescription: '' };

  ngOnInit(): void {
    const param = this.route.snapshot.paramMap.get('id');
    if (param && param !== 'new') {
      const id = Number(param);
      this.id.set(id);
      this.isNew.set(false);
      this.api.get(id).subscribe((a) => this.fill(a));
    }
  }

  doDraft(): void {
    if (!this.topic.trim()) return;
    this.drafting.set(true);
    this.draftError.set(null);
    this.api.draft(this.topic.trim(), this.draftLang, null).subscribe({
      next: (d) => {
        this.model.title = this.model.title || d.title;
        this.model.excerpt = d.excerpt;
        this.model.bodyHtml = d.bodyHtml;
        this.model.metaTitle = d.metaTitle;
        this.model.metaDescription = d.metaDescription;
        this.drafting.set(false);
      },
      error: (e) => {
        this.draftError.set(e?.status === 402
          ? "You're out of AI credits (or the blog is on a higher plan)."
          : e?.error?.message ?? 'Could not draft the article.');
        this.drafting.set(false);
      },
    });
  }

  save(publish: boolean): void {
    if (!this.model.title.trim()) return;
    this.saving.set(true);
    this.error.set(null);
    this.savedMsg.set(null);
    const req = this.id() ? this.api.update(this.id()!, this.model) : this.api.create(this.model);
    req.subscribe({
      next: (a) => {
        this.id.set(a.id);
        this.isNew.set(false);
        if (publish && a.status !== 'Published') {
          this.api.publish(a.id, true).subscribe(() => this.afterSave(true));
        } else {
          this.afterSave(publish);
        }
      },
      error: (e) => { this.error.set(e?.error?.message ?? 'Could not save the article.'); this.saving.set(false); },
    });
  }

  private afterSave(published: boolean): void {
    this.saving.set(false);
    this.savedMsg.set(published ? 'Saved & published.' : 'Saved.');
    if (this.isNew()) this.router.navigate(['/admin/articles', this.id()]);
  }

  private fill(a: Article): void {
    this.model = {
      title: a.title, slug: a.slug, excerpt: a.excerpt ?? '', bodyHtml: a.bodyHtml,
      coverImageUrl: a.coverImageUrl ?? '', authorName: a.authorName ?? '',
      metaTitle: a.metaTitle ?? '', metaDescription: a.metaDescription ?? '',
    };
  }
}
