import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SITE_URL } from '../../../core/api.config';
import { SeoService } from '../../../core/services/seo.service';

@Component({
  selector: 'app-contact',
  imports: [FormsModule],
  template: `
    <section class="page-container py-12">
      <div class="max-w-2xl mb-10">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900">Contact us</h1>
        <p class="mt-3 text-slate-600">Questions about an order, customization, or a bulk enquiry? We'd love to help.</p>
      </div>

      <div class="grid lg:grid-cols-3 gap-8">
        <!-- Details -->
        <div class="space-y-5">
          @for (c of details; track c.label) {
            <div class="flex items-start gap-3">
              <span class="text-xl">{{ c.icon }}</span>
              <div>
                <div class="text-sm font-semibold text-slate-800">{{ c.label }}</div>
                <div class="text-sm text-slate-500 whitespace-pre-line">{{ c.value }}</div>
              </div>
            </div>
          }
        </div>

        <!-- Form -->
        <div class="lg:col-span-2 bg-white border border-slate-200 rounded-2xl p-6">
          @if (sent()) {
            <div class="text-center py-10">
              <div class="text-4xl">✅</div>
              <h2 class="text-lg font-semibold text-slate-800 mt-2">Thanks for reaching out!</h2>
              <p class="text-slate-500 text-sm mt-1">We'll get back to you within 1 business day.</p>
              <button type="button" (click)="sent.set(false)" class="btn-ghost mt-4 border border-slate-300">Send another message</button>
            </div>
          } @else {
            <form (ngSubmit)="submit()" class="space-y-4">
              <div class="grid sm:grid-cols-2 gap-4">
                <div><label class="lbl">Name</label><input [(ngModel)]="form.name" name="name" required class="input" /></div>
                <div><label class="lbl">Email</label><input [(ngModel)]="form.email" name="email" type="email" required class="input" /></div>
              </div>
              <div><label class="lbl">Subject</label><input [(ngModel)]="form.subject" name="subject" class="input" /></div>
              <div><label class="lbl">Message</label><textarea [(ngModel)]="form.message" name="message" rows="5" required class="input"></textarea></div>
              <button type="submit" class="btn-primary">Send message</button>
              <p class="text-xs text-slate-400">This is a demo form — submissions aren't stored yet.</p>
            </form>
          }
        </div>
      </div>
    </section>
  `,
})
export class ContactComponent implements OnInit {
  private readonly seo = inject(SeoService);

  readonly sent = signal(false);
  form = { name: '', email: '', subject: '', message: '' };

  readonly details = [
    { icon: '📍', label: 'Address', value: 'CalendarShop\nChennai, Tamil Nadu, India' },
    { icon: '📞', label: 'Phone', value: '+91 62922 23322' },
    { icon: '✉️', label: 'Email', value: 'support@calendarshop.example' },
    { icon: '🕒', label: 'Hours', value: 'Mon–Sat, 9:30 AM – 6:30 PM' },
  ];

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'Contact us — CalendarShop',
      description: 'Get in touch with CalendarShop for orders, customization help, or bulk and corporate calendar enquiries.',
      url: `${SITE_URL}/contact`,
    });
  }

  submit(): void {
    if (!this.form.name.trim() || !this.form.email.trim() || !this.form.message.trim()) return;
    this.sent.set(true);
    this.form = { name: '', email: '', subject: '', message: '' };
  }
}
