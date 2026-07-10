import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeSection, ThemeService } from '../../core/services/theme.service';
import { SeoService } from '../../core/services/seo.service';
import { StorefrontSectionComponent } from './storefront-section.component';
import { SectionSlot, slotsFrom } from './section-slot';

interface EmptyStateCfg { heading?: string; body?: string; buttonText?: string; buttonLink?: string; }

/**
 * Themed 404 page (S7). Renders the published theme's `404` template: an EmptyState section
 * drives the message, and any other (static) sections render below. Falls back to a branded
 * default when the theme defines no 404 template — so unknown URLs get a real, on-brand page
 * instead of a silent redirect home.
 */
@Component({
  selector: 'app-not-found',
  imports: [RouterLink, StorefrontSectionComponent],
  template: `
    <section class="page-container py-20 text-center">
      <p class="text-6xl font-bold text-slate-200">404</p>
      <h1 class="text-2xl font-bold text-slate-900 mt-2">{{ empty().heading || 'Page not found' }}</h1>
      <p class="text-slate-500 mt-2">{{ empty().body || "The page you're looking for doesn't exist or has moved." }}</p>
      <a [routerLink]="empty().buttonLink || '/'" class="inline-block mt-6 btn-primary px-5 py-2.5">{{ empty().buttonText || 'Back to home' }}</a>
    </section>

    @for (slot of extraSlots(); track $index) {
      @if (slot.data) { <app-storefront-section [section]="slot.data" /> }
    }
  `,
})
export class NotFoundComponent implements OnInit {
  private readonly theme = inject(ThemeService);
  private readonly seo = inject(SeoService);

  private readonly sections = signal<ThemeSection[]>([]);

  /** EmptyState settings drive the headline/body/button when the theme provides them. */
  readonly empty = computed<EmptyStateCfg>(() => {
    const es = this.sections().find((s) => s.sectionType === 'EmptyState');
    try { return es?.settings ? JSON.parse(es.settings) : {}; } catch { return {}; }
  });
  /** Any non-EmptyState sections on the 404 template, rendered below the message. */
  readonly extraSlots = computed<SectionSlot[]>(() =>
    slotsFrom(this.sections().filter((s) => s.sectionType !== 'EmptyState'), []));

  ngOnInit(): void {
    this.seo.setMeta({ title: 'Page not found' });
    this.theme.getTemplate('404').subscribe((s) => this.sections.set(s));
  }
}
