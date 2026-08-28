import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Article, BlogService } from '../../../core/services/blog.service';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-blog-detail',
  imports: [RouterLink, DatePipe],
  template: `
    <div class="max-w-3xl mx-auto px-4 py-8">
      <a routerLink="/blog" class="text-sm text-primary hover:underline">← All posts</a>

      @if (loaded() && !article()) {
        <div class="text-center py-16">
          <h1 class="text-xl font-semibold text-slate-800">Post not found</h1>
          <a routerLink="/blog" class="text-primary hover:underline mt-2 inline-block">Back to the blog</a>
        </div>
      }

      @if (article(); as a) {
        <article class="mt-4">
          <h1 class="text-3xl font-bold text-slate-900 leading-tight">{{ a.title }}</h1>
          <div class="text-sm text-slate-400 mt-2">
            @if (a.authorName) { {{ a.authorName }} · } {{ a.publishedAt | date:'dd MMM yyyy' }}
          </div>
          @if (a.coverImageUrl) {
            <img [src]="a.coverImageUrl" [alt]="a.title" class="w-full rounded-xl mt-5" />
          }
          <div class="prose prose-slate max-w-none mt-6 blog-body" [innerHTML]="body()"></div>
        </article>
      }
    </div>
  `,
  styles: [`
    .blog-body ::ng-deep h2 { font-size: 1.35rem; font-weight: 700; margin: 1.5rem 0 .5rem; color: #1e293b; }
    .blog-body ::ng-deep h3 { font-size: 1.1rem; font-weight: 600; margin: 1.2rem 0 .4rem; color: #334155; }
    .blog-body ::ng-deep p { margin: .75rem 0; color: #475569; line-height: 1.7; }
    .blog-body ::ng-deep ul { list-style: disc; padding-left: 1.5rem; margin: .75rem 0; color: #475569; }
    .blog-body ::ng-deep li { margin: .25rem 0; }
  `],
})
export class BlogDetailComponent implements OnInit {
  private readonly api = inject(BlogService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly theme = inject(ThemeService);

  readonly article = signal<Article | null>(null);
  readonly loaded = signal(false);
  readonly body = signal<SafeHtml>('');

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('slug') ?? '';
    this.api.getBySlug(slug).subscribe((a) => {
      this.article.set(a);
      this.loaded.set(true);
      if (!a) { this.seo.setMeta({ title: 'Post not found', noindex: true }); return; }

      this.body.set(this.sanitizer.bypassSecurityTrustHtml(a.bodyHtml));
      this.seo.setMeta({
        title: a.metaTitle || a.title,
        description: a.metaDescription || a.excerpt || '',
        image: a.coverImageUrl || undefined,
        type: 'article',
        url: `/blog/${a.slug}`,
      });
      this.seo.setJsonLd({
        '@context': 'https://schema.org',
        '@type': 'BlogPosting',
        headline: a.title,
        image: a.coverImageUrl || undefined,
        datePublished: a.publishedAt,
        dateModified: a.updatedAt || a.publishedAt,
        author: { '@type': a.authorName ? 'Person' : 'Organization', name: a.authorName || this.theme.storeName() },
        publisher: { '@type': 'Organization', name: this.theme.storeName() },
        description: a.metaDescription || a.excerpt || undefined,
      });
    });
  }
}
