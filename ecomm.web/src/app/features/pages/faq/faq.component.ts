import { Component, OnInit, inject, signal } from '@angular/core';
import { SITE_URL } from '../../../core/api.config';
import { SeoService } from '../../../core/services/seo.service';

interface Faq { q: string; a: string; }

@Component({
  selector: 'app-faq',
  template: `
    <section class="page-container py-12">
      <div class="max-w-3xl mx-auto">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900 text-center">Frequently asked questions</h1>
        <p class="mt-3 text-slate-600 text-center">Everything you need to know about ordering customized calendars.</p>

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

  readonly open = signal<number>(0);

  readonly faqs: Faq[] = [
    { q: 'How do I customize my calendar?', a: 'Open any product and choose "Upload design" to add your own artwork, or pick a template and add your photos, text, brand name and logo.' },
    { q: 'What sizes and finishes are available?', a: 'It depends on the product — most wall calendars come in A4 and A3 with glossy or matte finishes. The exact options are listed on each product page.' },
    { q: 'Do you offer bulk / corporate pricing?', a: 'Yes. We specialise in bulk corporate gifting with tiered pricing. Use the Contact page or call us for a quote.' },
    { q: 'How long does delivery take?', a: 'Standard orders are printed and shipped within a few business days, with pan-India delivery. Same-day delivery is available in select cities.' },
    { q: 'What are the shipping charges?', a: 'Shipping is calculated at checkout based on your pincode, and many products ship free above a threshold.' },
    { q: 'Can I return or replace a calendar?', a: 'Personalized products are made to order, but if your item arrives damaged or defective we will replace it — just reach out within 7 days.' },
    { q: 'What payment methods do you accept?', a: 'We accept all major UPI apps, cards, net-banking and wallets via our secure payment gateway.' },
    { q: 'Can I see a proof before printing?', a: 'For custom and bulk orders, our team can share a digital proof for approval before we go to print.' },
  ];

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'FAQ — CalendarShop',
      description: 'Answers to common questions about customizing, ordering, shipping, returns and bulk pricing for CalendarShop calendars.',
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
