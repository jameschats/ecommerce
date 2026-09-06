import { Component, input } from '@angular/core';
import { Testimonial } from '../../core/models/testimonial.model';

/**
 * Home page testimonials — a plain responsive grid of quote cards. Unlike the gallery strip
 * this doesn't auto-scroll: a testimonial is read, not skimmed past, and there are usually
 * few enough of them that a grid shows them all without needing motion. Renders nothing
 * when the admin hasn't added any.
 */
@Component({
  selector: 'app-testimonial-section',
  template: `
    @if (items().length) {
      <section class="page-container py-8">
        <h2 class="text-lg sm:text-xl font-bold text-slate-900 mb-4">What our customers say</h2>
        <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (t of items(); track t.testimonialId) {
            <div class="bg-white border border-slate-200 rounded-xl p-5">
              <div class="text-amber-400 text-sm" aria-hidden="true">
                @for (i of stars(5); track i) { <span>{{ i <= t.rating ? '★' : '☆' }}</span> }
              </div>
              <p class="text-sm text-slate-600 mt-2">“{{ t.quote }}”</p>
              <div class="flex items-center gap-3 mt-4">
                @if (t.photoUrl) {
                  <img [src]="t.photoUrl" [alt]="t.name" class="w-9 h-9 rounded-full object-cover shrink-0" loading="lazy" />
                } @else {
                  <span class="w-9 h-9 rounded-full bg-primary/10 text-primary grid place-items-center font-semibold shrink-0">
                    {{ t.name.charAt(0).toUpperCase() }}
                  </span>
                }
                <div class="min-w-0">
                  <div class="text-sm font-semibold text-slate-800 truncate">{{ t.name }}</div>
                  @if (t.roleOrCompany) { <div class="text-xs text-slate-400 truncate">{{ t.roleOrCompany }}</div> }
                </div>
              </div>
            </div>
          }
        </div>
      </section>
    }
  `,
})
export class TestimonialSectionComponent {
  readonly items = input.required<Testimonial[]>();

  stars(n: number): number[] {
    return Array.from({ length: n }, (_, i) => i + 1);
  }
}
