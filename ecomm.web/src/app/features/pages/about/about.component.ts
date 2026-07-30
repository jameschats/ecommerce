import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { SeoService } from '../../../core/services/seo.service';

@Component({
  selector: 'app-about',
  imports: [RouterLink],
  template: `
    <section class="page-container py-12">
      <div class="max-w-3xl">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900">About CalendarShop</h1>
        <p class="mt-4 text-lg text-slate-600">
          We help businesses and individuals turn 2026 into something personal — premium, fully customizable
          calendars printed with your photos, brand name and logo.
        </p>
        <p class="mt-4 text-slate-600">
          From a single wall calendar to bulk corporate gifting, we obsess over print quality, on-time delivery
          and a buying experience that's genuinely simple. What started as a small print shop is now trusted by
          teams across India for their year-round calendar needs.
        </p>
      </div>

      <!-- Stats -->
      <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mt-10">
        @for (s of stats; track s.label) {
          <div class="bg-white border border-slate-200 rounded-xl p-5 text-center">
            <div class="text-2xl font-bold text-primary">{{ s.value }}</div>
            <div class="text-sm text-slate-500 mt-1">{{ s.label }}</div>
          </div>
        }
      </div>

      <!-- Values -->
      <h2 class="text-xl font-bold text-slate-900 mt-12 mb-4">What we stand for</h2>
      <div class="grid sm:grid-cols-2 lg:grid-cols-4 gap-4">
        @for (v of values; track v.title) {
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <div class="text-2xl">{{ v.icon }}</div>
            <h3 class="font-semibold text-slate-800 mt-2">{{ v.title }}</h3>
            <p class="text-sm text-slate-500 mt-1">{{ v.text }}</p>
          </div>
        }
      </div>

      <div class="mt-12 bg-gradient-to-br from-primary to-primary-dark rounded-2xl px-8 py-10 text-center text-white">
        <h2 class="text-2xl font-bold">Ready to design your 2026 calendar?</h2>
        <p class="mt-2 text-white/85">Pick a style, add your photos, and we'll handle the rest.</p>
        <a routerLink="/order" class="inline-block mt-5 bg-white text-primary-dark font-medium px-6 py-2.5 rounded-lg hover:bg-slate-100 transition">Shop calendars</a>
      </div>
    </section>
  `,
})
export class AboutComponent implements OnInit {
  private readonly seo = inject(SeoService);

  readonly stats = [
    { value: '10,000+', label: 'Calendars printed' },
    { value: '1,000+', label: 'Happy customers' },
    { value: '4.5★', label: 'Average rating' },
    { value: 'Pan-India', label: 'Delivery' },
  ];

  readonly values = [
    { icon: '🖨️', title: 'Premium print quality', text: 'Full-colour HD printing on heavy art paper.' },
    { icon: '⏱️', title: 'On-time delivery', text: 'We ship on schedule, every time.' },
    { icon: '🎨', title: 'Easy customization', text: 'Your photos, text, brand name and logo.' },
    { icon: '📦', title: 'Bulk-friendly', text: 'Great pricing for corporate gifting at scale.' },
  ];

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'About Us — CalendarShop',
      description: 'CalendarShop makes premium, fully customizable 2026 calendars — trusted by teams across India for quality printing and on-time delivery.',
      url: `${SITE_URL}/about`,
    });
  }
}
