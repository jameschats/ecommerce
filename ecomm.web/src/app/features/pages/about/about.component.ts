import { Component, OnInit, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-about',
  imports: [RouterLink],
  template: `
    <section class="page-container py-12">
      <div class="max-w-3xl">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900">About {{ store() }}</h1>
        <p class="mt-4 text-lg text-slate-600">
          We're on a simple mission: bring you products you'll love, at fair prices, delivered fast — with a
          shopping experience that's genuinely easy from browse to doorstep.
        </p>
        <p class="mt-4 text-slate-600">
          From your first order to your hundredth, we obsess over quality, honest pricing and dependable delivery.
          Thanks for shopping with {{ store() }}.
        </p>
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
        <h2 class="text-2xl font-bold">Ready to shop?</h2>
        <p class="mt-2 text-white/85">Discover our latest products and find something you love.</p>
        <a routerLink="/products" class="inline-block mt-5 bg-white text-primary-dark font-medium px-6 py-2.5 rounded-lg hover:bg-slate-100 transition">Shop now</a>
      </div>
    </section>
  `,
})
export class AboutComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly theme = inject(ThemeService);

  readonly store = computed(() => this.theme.storeName() || 'our store');

  readonly values = [
    { icon: '✨', title: 'Quality you can trust', text: 'Products we are proud to stand behind.' },
    { icon: '🚚', title: 'Fast, reliable delivery', text: 'Shipped quickly and tracked to your door.' },
    { icon: '🛒', title: 'Easy, secure shopping', text: 'Simple checkout with protected payments.' },
    { icon: '💬', title: 'Support that cares', text: 'Real help whenever you need it.' },
  ];

  ngOnInit(): void {
    const name = this.theme.storeName() || 'our store';
    this.seo.setMeta({
      title: `About us — ${name}`,
      description: `Learn about ${name} — quality products, fair prices and fast, reliable delivery.`,
      url: `${SITE_URL}/about`,
    });
  }
}
