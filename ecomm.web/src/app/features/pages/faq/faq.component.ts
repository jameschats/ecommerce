import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { SITE_URL } from '../../../core/api.config';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

interface Faq { q: string; a: string; }

@Component({
  selector: 'app-faq',
  template: `
    <section class="page-container py-12">
      <div class="max-w-3xl mx-auto">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900 text-center">Frequently asked questions</h1>
        <p class="mt-3 text-slate-600 text-center">Everything you need to know about ordering from {{ store() }}.</p>

        <div class="mt-8 divide-y divide-slate-200 border border-slate-200 rounded-2xl bg-white overflow-hidden">
          @for (item of faqs; track item.q; let i = $index) {
            <div>
              <button type="button" (click)="toggle(i)" class="w-full flex items-center justify-between gap-4 px-5 py-4 text-left hover:bg-slate-50">
                <span class="font-medium text-slate-800">{{ item.q }}</span>
                <span class="text-slate-400 text-xl shrink-0">{{ open() === i ? '−' : '+' }}</span>
              </button>
              @if (open() === i) {
                <div class="px-5 pb-4 -mt-1 text-sm text-slate-600">{{ item.a }}</div>
              }
            </div>
          }
        </div>
      </div>
    </section>
  `,
})
export class FaqComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly theme = inject(ThemeService);

  readonly store = computed(() => this.theme.storeName() || 'our store');
  readonly open = signal<number>(0);

  readonly faqs: Faq[] = [
    { q: 'How do I place an order?', a: 'Browse our products, add what you like to your cart, then check out with your delivery address and a payment method. You will get an order confirmation right away.' },
    { q: 'How long does delivery take?', a: 'Most orders are dispatched within a few business days. The exact delivery estimate for your location is shown at checkout and in your order confirmation.' },
    { q: 'What are the shipping charges?', a: 'Shipping is calculated at checkout based on your pincode, and many orders qualify for free shipping above a threshold.' },
    { q: 'What payment methods do you accept?', a: 'We accept major UPI apps, cards, net-banking and wallets through our secure payment gateway — plus Cash on Delivery where available.' },
    { q: 'How do I track my order?', a: 'Sign in and open Orders in your account to see live status, or use the tracking link in your confirmation email/SMS.' },
    { q: 'Can I return or replace an item?', a: 'Yes. If an item arrives damaged or defective, or is eligible under our return policy, reach out within the returns window and we will help with a replacement or refund.' },
    { q: 'How do I contact support?', a: 'Head to the Contact page and send us a message — we typically reply within one business day.' },
  ];

  ngOnInit(): void {
    const name = this.theme.storeName() || 'our store';
    this.seo.setMeta({
      title: `FAQ — ${name}`,
      description: `Answers to common questions about ordering, shipping, payment, returns and support at ${name}.`,
      url: `${SITE_URL}/faq`,
    });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: this.faqs.map((f) => ({
        '@type': 'Question',
        name: f.q,
        acceptedAnswer: { '@type': 'Answer', text: f.a },
      })),
    });
  }

  toggle(i: number): void {
    this.open.set(this.open() === i ? -1 : i);
  }
}
