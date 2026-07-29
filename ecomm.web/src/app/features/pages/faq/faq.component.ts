import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { API_BASE_URL, SITE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

interface Faq { faqId: number; question: string; answer: string; category: string | null; }

@Component({
  selector: 'app-faq',
  imports: [RouterLink],
  template: `
    <section class="page-container py-12">
      <div class="max-w-3xl mx-auto">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900 text-center">Frequently asked questions</h1>
        <p class="mt-3 text-slate-600 text-center">Everything you need to know about ordering from {{ store() }}.</p>

        @if (faqs().length) {
          <div class="mt-8 divide-y divide-slate-200 border border-slate-200 rounded-2xl bg-white overflow-hidden">
            @for (item of faqs(); track item.faqId; let i = $index) {
              <div>
                <button type="button" (click)="toggle(i)" class="w-full flex items-center justify-between gap-4 px-5 py-4 text-left hover:bg-slate-50">
                  <span class="font-medium text-slate-800">{{ item.question }}</span>
                  <span class="text-slate-400 text-xl shrink-0">{{ open() === i ? '−' : '+' }}</span>
                </button>
                @if (open() === i) {
                  <div class="px-5 pb-4 -mt-1 text-sm text-slate-600 whitespace-pre-line">{{ item.answer }}</div>
                }
              </div>
            }
          </div>
        } @else if (!loading()) {
          <p class="mt-8 text-center text-slate-500 text-sm">
            No questions have been published yet. <a routerLink="/contact" class="text-primary hover:underline">Contact us</a> and we'll help.
          </p>
        }
      </div>
    </section>
  `,
})
export class FaqComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly theme = inject(ThemeService);
  private readonly http = inject(HttpClient);
  private readonly siteUrl = inject(SITE_URL);

  readonly store = computed(() => this.theme.storeName() || 'our store');
  readonly open = signal<number>(0);
  readonly loading = signal(true);
  readonly faqs = signal<Faq[]>([]);

  ngOnInit(): void {
    const name = this.theme.storeName() || 'our store';
    this.seo.setMeta({
      title: `FAQ — ${name}`,
      description: `Answers to common questions about ordering, shipping, payment, returns and support at ${name}.`,
      url: `${this.siteUrl}/faq`,
    });

    this.http.get<ApiResponse<Faq[]>>(`${API_BASE_URL}/faq`).subscribe({
      next: (r) => {
        const items = r.data ?? [];
        this.faqs.set(items);
        this.loading.set(false);
        // Structured data has to follow the real content, not a constant, or the
        // markup and the page disagree — which is worse for SEO than omitting it.
        if (items.length) {
          this.seo.setJsonLd({
            '@context': 'https://schema.org',
            '@type': 'FAQPage',
            mainEntity: items.map((f) => ({
              '@type': 'Question',
              name: f.question,
              acceptedAnswer: { '@type': 'Answer', text: f.answer },
            })),
          });
        }
      },
      error: () => this.loading.set(false),
    });
  }

  toggle(i: number): void {
    this.open.set(this.open() === i ? -1 : i);
  }
}
