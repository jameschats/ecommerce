import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { Testimonial } from '../../core/models/testimonial.model';
import { TestimonialService } from '../../core/services/testimonial.service';

export interface TestimonialsData {
  items: Testimonial[];
}

/** Preloads the home page testimonials before route activation — same SSR-hydration rationale as homeResolver. */
export const testimonialsResolver: ResolveFn<TestimonialsData> = () => {
  const testimonials = inject(TestimonialService);
  return testimonials.getAll().pipe(
    map((items) => ({ items })),
    catchError(() => of({ items: [] as Testimonial[] })),
  );
};
