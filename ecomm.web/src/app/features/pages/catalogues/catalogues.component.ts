import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { Catalogue } from '../../../core/models/catalogue.model';
import { ContentPage } from '../../../core/models/content-page.model';
import { CatalogueService } from '../../../core/services/catalogue.service';
import { SeoService } from '../../../core/services/seo.service';

/** Downloadable PDF catalogues — the design booklets a dealer browses offline before ordering
 *  by design number. Plain download list, same card language as the rest of the site. Title,
 *  intro and search title/description are CMS-editable (066), same as Contact/FAQ/Buying guide. */
@Component({
  selector: 'app-catalogues',
  template: `
    <section class="page-container py-10 sm:py-14">
      <div class="max-w-4xl mx-auto">
        <div class="max-w-2xl mx-auto text-center mb-8 sm:mb-10">
          <h1 class="text-2xl sm:text-3xl font-bold text-slate-900">{{ page?.title || 'Catalogues' }}</h1>
          @if (intro) {
            <div class="mt-3 text-slate-600 intro-copy" [innerHTML]="intro"></div>
          } @else {
            <p class="mt-3 text-slate-600">
              Download our design catalogues as PDF — browse every design number offline, then order by design number on the price list.
            </p>
          }
        </div>

        @if (loading()) {
          <div class="grid sm:grid-cols-2 gap-4">
            @for (s of [0, 1, 2, 3]; track s) {
              <div class="h-20 rounded-xl border border-slate-200 bg-slate-50 animate-pulse"></div>
            }
          </div>
        } @else if (!catalogues().length) {
          <p class="text-center text-slate-400 py-10">No catalogues are available right now.</p>
        } @else {
          <div class="grid sm:grid-cols-2 gap-4">
            @for (c of catalogues(); track c.catalogueId) {
              <a [href]="c.fileUrl" [download]="c.fileName" target="_blank" rel="noopener"
                 class="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 hover:border-primary hover:shadow-sm transition">
                <div class="w-11 h-11 shrink-0 rounded-lg bg-red-50 text-red-600 grid place-items-center text-[10px] font-bold">PDF</div>
                <div class="flex-1 min-w-0">
                  <p class="font-medium text-slate-800 truncate">{{ c.title }}</p>
                  <p class="text-xs text-slate-400">{{ formatSize(c.fileSizeBytes) }}</p>
                </div>
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                     class="text-slate-400 shrink-0" aria-hidden="true">
                  <path d="M12 3v12" /><path d="m7 10 5 5 5-5" /><path d="M5 21h14" />
                </svg>
              </a>
            }
          </div>
        }
      </div>
    </section>
  `,
  styles: [`
    .intro-copy :is(p, ul, ol) { margin-block: 0.6rem; }
    .intro-copy :is(ul, ol) { padding-inline-start: 1.4rem; }
    .intro-copy ul { list-style: disc; }
    .intro-copy ol { list-style: decimal; }
    .intro-copy a { color: var(--color-primary, #2563eb); text-decoration: underline; }
  `],
})
export class CataloguesComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly svc = inject(CatalogueService);
  private readonly seo = inject(SeoService);

  readonly page = this.route.snapshot.data['page'] as ContentPage | null;
  readonly intro = (this.page?.sections ?? []).find((s) => s.sectionType === 'Prose')?.content ?? null;

  readonly catalogues = signal<Catalogue[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.seo.setMeta({
      title: this.page?.metaTitle || 'Catalogues — Download design catalogues (PDF)',
      description: this.page?.metaDescription
        ?? 'Download our calendar design catalogues as PDF — browse every design number offline before ordering.',
      url: `${SITE_URL}/catalogues`,
    });
    this.svc.list().subscribe({
      next: (c) => { this.catalogues.set(c); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  formatSize(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.round(bytes / 1024)} KB`;
  }
}
