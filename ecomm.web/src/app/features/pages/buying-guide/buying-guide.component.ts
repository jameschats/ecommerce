import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal, viewChildren } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { ContentPage, ContentSection } from '../../../core/models/content-page.model';
import { SeoService } from '../../../core/services/seo.service';

/** A step as the page presents it: a number, a heading, and the body written in admin. */
interface Step {
  id: string;
  number: number | null;
  heading: string;
  body: string;
}

/**
 * How to choose what to order — the page a dealer reads before their first order.
 *
 * Presented as a numbered run of steps rather than a stack of headings, with a contents list
 * that follows along on desktop. All of it is driven by the sections written in admin (055);
 * nothing here assumes how many steps there are or what they are called.
 */
@Component({
  selector: 'app-buying-guide',
  imports: [RouterLink],
  template: `
    <section class="page-container py-10 sm:py-14">
      <div class="max-w-6xl mx-auto">

        <header class="max-w-3xl">
          <h1 class="text-3xl sm:text-4xl font-bold text-slate-900">{{ page?.title || 'Buying guide' }}</h1>
          @if (page?.metaDescription) {
            <p class="mt-3 text-lg text-slate-600">{{ page!.metaDescription }}</p>
          }
        </header>

        @if (steps().length) {
          <div class="mt-10 grid lg:grid-cols-[minmax(0,1fr)_240px] gap-10 lg:gap-14 items-start">

            <!-- Steps -->
            <ol class="relative space-y-10 list-none">
              <!--
                The connecting line stops at the last marker rather than running the full
                height, so it reads as joining the steps instead of trailing off the page.
              -->
              <span class="hidden sm:block absolute left-[19px] top-3 bottom-3 w-px bg-slate-200" aria-hidden="true"></span>

              @for (s of steps(); track s.id) {
                <li #stepEl [attr.id]="s.id" class="relative sm:pl-14 scroll-mt-28">
                  <span class="hidden sm:flex absolute left-0 top-0 w-10 h-10 rounded-full items-center justify-center
                               text-sm font-semibold ring-4 ring-white"
                        [class]="s.number !== null ? 'text-white' : 'bg-slate-100 text-slate-500'"
                        [style.background]="s.number !== null ? 'var(--color-primary)' : null"
                        aria-hidden="true">
                    {{ s.number ?? '•' }}
                  </span>

                  <h2 class="text-xl sm:text-2xl font-bold text-slate-900 leading-snug">
                    @if (s.number !== null) {
                      <span class="sm:hidden text-primary">{{ s.number }}. </span>
                    }
                    {{ s.heading }}
                  </h2>
                  <div class="mt-2 text-slate-600 guide-body" [innerHTML]="s.body"></div>
                </li>
              }
            </ol>

            <!-- Contents. Hidden on mobile, where it would just be the page again. -->
            <nav class="hidden lg:block sticky top-28" aria-label="On this page">
              <p class="text-[11px] font-semibold uppercase tracking-wider text-slate-400 mb-3">On this page</p>
              <ul class="space-y-1 text-sm border-l border-slate-200">
                @for (s of steps(); track s.id) {
                  <li>
                    <a [href]="'#' + s.id" (click)="jump($event, s.id)"
                       class="block -ml-px border-l-2 pl-3 py-1 transition"
                       [class]="active() === s.id
                          ? 'border-current text-primary font-medium'
                          : 'border-transparent text-slate-500 hover:text-slate-800'">
                      {{ s.heading }}
                    </a>
                  </li>
                }
              </ul>

              <a routerLink="/order"
                 class="mt-6 flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg text-white
                        text-sm font-medium transition hover:opacity-90"
                 [style.background]="'var(--color-primary)'">
                Order now →
              </a>
              <p class="mt-2 text-xs text-slate-400">Set quantities against the price list.</p>
            </nav>
          </div>

          <!-- Closing prompt: the guide exists to end in an order. -->
          <div class="mt-14 rounded-2xl border border-slate-200 bg-white px-6 py-8 sm:px-10 text-center">
            <h2 class="text-xl sm:text-2xl font-bold text-slate-900">Ready to order?</h2>
            <p class="mt-2 text-slate-600">Open the price list, set your quantities, and we will price it as you type.</p>
            <a routerLink="/order"
               class="inline-block mt-5 px-6 py-2.5 rounded-lg text-white font-medium transition hover:opacity-90"
               [style.background]="'var(--color-primary)'">
              Go to the price list
            </a>
          </div>
        } @else {
          <p class="mt-6 text-slate-500">This guide is being written. Please check back shortly.</p>
        }
      </div>
    </section>
  `,
  styles: [`
    .guide-body :is(p, ul, ol) { margin-block: 0.65rem; }
    .guide-body :is(ul, ol) { padding-inline-start: 1.35rem; }
    .guide-body ul { list-style: disc; }
    .guide-body ol { list-style: decimal; }
    .guide-body li { margin-block: 0.3rem; }
    .guide-body li::marker { color: var(--color-primary, #2563eb); }
    .guide-body :is(h3, h4) { font-weight: 600; margin-block: 0.9rem 0.3rem; color: rgb(30 41 59); }
    .guide-body strong { font-weight: 600; color: rgb(30 41 59); }
    .guide-body a { color: var(--color-primary, #2563eb); text-decoration: underline; }
  `],
})
export class BuyingGuideComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly page = this.route.snapshot.data['page'] as ContentPage | null;
  private readonly stepEls = viewChildren<ElementRef<HTMLElement>>('stepEl');
  readonly active = signal('');

  /**
   * "Step 1: Choose your order category" becomes a numbered marker and a clean heading, so the
   * number is not repeated in both. A section titled anything else keeps its title in full and
   * gets a plain marker — retitling a step in admin must not break the page.
   */
  readonly steps = computed<Step[]>(() =>
    (this.page?.sections ?? [])
      .filter((s: ContentSection) => s.sectionType === 'Prose')
      .map((s, i) => {
        const title = (s.title ?? '').trim();
        const m = /^step\s+(\d+)\s*[:.\-–]\s*(.+)$/i.exec(title);
        return {
          id: `step-${i + 1}`,
          number: m ? Number(m[1]) : null,
          heading: m ? m[2].trim() : (title || `Step ${i + 1}`),
          body: s.content ?? '',
        };
      }));

  private observer?: IntersectionObserver;

  ngOnInit(): void {
    this.seo.setMeta({
      title: this.page?.metaTitle || 'Buying guide',
      description: this.page?.metaDescription
        ?? 'How to choose the right calendar: order type, size, paper quality and layout.',
      url: `${SITE_URL}/buying-guide`,
    });

    if (this.isBrowser) this.watchSteps();
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  /**
   * Highlights the contents entry for whichever step is in view. An observer rather than a
   * scroll handler: the browser does the work off the main thread, and a guide is a page
   * people scroll slowly through.
   */
  private watchSteps(): void {
    queueMicrotask(() => {
      const els = this.stepEls().map((r) => r.nativeElement);
      if (!els.length) return;

      this.observer = new IntersectionObserver(
        (entries) => {
          const visible = entries
            .filter((e) => e.isIntersecting)
            .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)[0];
          if (visible?.target.id) this.active.set(visible.target.id);
        },
        // Biased to the upper part of the viewport, so the highlight matches what is being
        // read rather than whatever happens to be entering from the bottom.
        { rootMargin: '-96px 0px -55% 0px', threshold: 0 },
      );
      els.forEach((el) => this.observer!.observe(el));
    });
  }

  /** Smooth-scrolls without leaving a #hash on every click. */
  jump(event: Event, id: string): void {
    if (!this.isBrowser) return;
    event.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    this.active.set(id);
  }
}
