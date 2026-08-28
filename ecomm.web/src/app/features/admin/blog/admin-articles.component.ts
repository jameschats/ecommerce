import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ArticleSummary, BlogService } from '../../../core/services/blog.service';

@Component({
  selector: 'app-admin-articles',
  imports: [RouterLink, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 class="text-xl font-bold text-slate-900">📝 Blog</h1>
          <p class="text-sm text-slate-500">Write articles (or let AI draft them) to bring shoppers in from search.</p>
        </div>
        <a routerLink="/admin/articles/new" class="btn-primary text-sm shrink-0">New article</a>
      </div>

      @if (loaded() && articles().length === 0) {
        <div class="rounded-xl border border-slate-200 bg-white p-8 text-center text-slate-500">
          <div class="text-3xl">📝</div>
          <p class="mt-2">No articles yet.</p>
          <a routerLink="/admin/articles/new" class="text-primary hover:underline mt-1 inline-block">Write your first post →</a>
        </div>
      } @else {
        <div class="space-y-2">
          @for (a of articles(); track a.id) {
            <div class="bg-white border border-slate-200 rounded-lg p-3 flex items-center justify-between gap-3">
              <div class="min-w-0">
                <a [routerLink]="['/admin/articles', a.id]" class="text-sm font-medium text-slate-800 hover:text-primary truncate block">{{ a.title }}</a>
                <div class="text-xs text-slate-400">
                  <span class="px-1.5 py-0.5 rounded-full" [class]="a.status === 'Published' ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-600'">{{ a.status }}</span>
                  @if (a.publishedAt) { · {{ a.publishedAt | date:'dd MMM yyyy' }} }
                </div>
              </div>
              <div class="flex items-center gap-3 shrink-0">
                @if (a.status === 'Published') {
                  <button type="button" (click)="setPublished(a, false)" class="text-sm text-slate-500 hover:underline">Unpublish</button>
                } @else {
                  <button type="button" (click)="setPublished(a, true)" class="text-sm text-emerald-600 hover:underline">Publish</button>
                }
                <a [routerLink]="['/admin/articles', a.id]" class="text-sm text-primary hover:underline">Edit</a>
                <button type="button" (click)="remove(a)" class="text-sm text-red-600 hover:underline">Delete</button>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminArticlesComponent implements OnInit {
  private readonly api = inject(BlogService);
  readonly articles = signal<ArticleSummary[]>([]);
  readonly loaded = signal(false);

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.api.list(1, 100).subscribe((r) => { this.articles.set(r.items); this.loaded.set(true); });
  }

  setPublished(a: ArticleSummary, published: boolean): void {
    this.api.publish(a.id, published).subscribe(() => this.load());
  }
  remove(a: ArticleSummary): void {
    if (!confirm(`Delete "${a.title}"? This can't be undone.`)) return;
    this.api.remove(a.id).subscribe(() => this.load());
  }
}
