import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ArticleSummary, BlogService } from '../../../core/services/blog.service';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-blog-list',
  imports: [RouterLink, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto px-4 py-8">
      <h1 class="text-2xl font-bold text-slate-900 mb-1">Blog</h1>
      <p class="text-slate-500 mb-6">Guides, tips and stories from {{ storeName() }}.</p>

      @if (loaded() && articles().length === 0) {
        <p class="text-slate-400">No posts yet — check back soon.</p>
      }

      <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
        @for (a of articles(); track a.id) {
          <a [routerLink]="['/blog', a.slug]" class="group block rounded-xl border border-slate-200 overflow-hidden hover:shadow-md transition bg-white">
            @if (a.coverImageUrl) {
              <div class="aspect-[16/9] bg-slate-50 overflow-hidden">
                <img [src]="a.coverImageUrl" [alt]="a.title" class="w-full h-full object-cover group-hover:scale-105 transition" />
              </div>
            }
            <div class="p-4">
              <h2 class="font-semibold text-slate-800 group-hover:text-primary line-clamp-2">{{ a.title }}</h2>
              @if (a.excerpt) { <p class="text-sm text-slate-500 mt-1 line-clamp-3">{{ a.excerpt }}</p> }
              <div class="text-xs text-slate-400 mt-3">
                @if (a.authorName) { {{ a.authorName }} · } {{ a.publishedAt | date:'dd MMM yyyy' }}
              </div>
            </div>
          </a>
        }
      </div>
    </div>
  `,
})
export class BlogListComponent implements OnInit {
  private readonly api = inject(BlogService);
  private readonly seo = inject(SeoService);
  private readonly theme = inject(ThemeService);

  readonly articles = signal<ArticleSummary[]>([]);
  readonly loaded = signal(false);
  readonly storeName = this.theme.storeName;

  ngOnInit(): void {
    this.seo.setMeta({ title: `Blog · ${this.storeName()}`, description: `Guides, tips and stories from ${this.storeName()}.`, url: '/blog' });
    this.api.listPublic(1, 24).subscribe((r) => { this.articles.set(r.items); this.loaded.set(true); });
  }
}
