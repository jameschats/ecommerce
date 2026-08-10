import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { ContentPage, ContentSection } from '../../../core/models/content-page.model';
import { SeoService } from '../../../core/services/seo.service';

/**
 * The questions, now editable from admin (055) instead of a hardcoded array.
 *
 * The FAQPage structured data is kept and built from whatever is published, so adding a
 * question in admin adds it to what Google and AI assistants can quote. Answers are sanitised
 * HTML from the API, so they may carry links and emphasis.
 */
@Component({
  selector: 'app-faq',
  template: `
    <section class="page-container py-12">
      <div class="max-w-3xl mx-auto">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900 text-center">{{ page?.title || 'Frequently asked questions' }}</h1>
        <p class="mt-3 text-slate-600 text-center">Everything you need to know about ordering.</p>

        @if (items.length) {
          <div class="mt-8 divide-y divide-slate-200 border border-slate-200 rounded-2xl bg-white overflow-hidden">
            @for (item of items; track item.sectionId; let i = $index) {
              <div>
                <button type="button" (click)="toggle(i)" class="w-full flex items-center justify-between gap-4 px-5 py-4 text-left hover:bg-slate-50">
                  <span class="font-medium text-slate-800">{{ item.title }}</span>
                  <span class="text-slate-400 text-xl shrink-0">{{ open() === i ? '−' : '+' }}</span>
                </button>
                @if (open() === i) {
                  <div class="px-5 pb-4 -mt-1 text-sm text-slate-600 faq-answer" [innerHTML]="item.content"></div>
                }
              </div>
            }
          </div>
        } @else {
          <p class="mt-8 text-center text-slate-500">No questions have been published yet.</p>
        }
      </div>
    </section>
  `,
  styles: [`
    .faq-answer :is(p, ul, ol) { margin-block: 0.4rem; }
    .faq-answer :is(ul, ol) { padding-inline-start: 1.3rem; }
    .faq-answer ul { list-style: disc; }
    .faq-answer ol { list-style: decimal; }
    .faq-answer a { color: var(--color-primary, #2563eb); text-decoration: underline; }
  `],
})
export class FaqComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);

  readonly open = signal(0);
  readonly page = this.route.snapshot.data['page'] as ContentPage | null;
  readonly items: ContentSection[] = (this.page?.sections ?? []).filter((s) => s.sectionType === 'Faq');

  ngOnInit(): void {
    this.seo.setMeta({
      title: this.page?.metaTitle || 'FAQ',
      description: this.page?.metaDescription
        ?? 'Answers to common questions about ordering, delivery, returns and bulk pricing.',
      url: `${SITE_URL}/faq`,
    });

    if (this.items.length) {
      this.seo.setJsonLd({
        '@context': 'https://schema.org',
        '@type': 'FAQPage',
        mainEntity: this.items.map((f) => ({
          '@type': 'Question',
          name: f.title,
          // Structured data wants the answer as text, not markup.
          acceptedAnswer: { '@type': 'Answer', text: this.plain(f.content) },
        })),
      });
    }
  }

  toggle(i: number): void {
    this.open.set(this.open() === i ? -1 : i);
  }

  /** Strips the tags the editor may have added, leaving the sentence itself. */
  private plain(html: string | null): string {
    return (html ?? '').replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
  }
}
