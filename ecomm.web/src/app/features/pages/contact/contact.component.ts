import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SITE_URL } from '../../../core/api.config';
import { ContactService } from '../../../core/services/contact.service';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-contact',
  imports: [FormsModule],
  template: `
    <section class="page-container py-12">
      <div class="max-w-2xl mb-10">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900">Contact {{ store() }}</h1>
        <p class="mt-3 text-slate-600">Questions about an order, a product, or anything else? We'd love to help.</p>
      </div>

      <div class="grid lg:grid-cols-3 gap-8">
        <!-- Details -->
        <div class="space-y-5">
          @for (c of details(); track c.label) {
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
              <div><label class="lbl">Phone <span class="text-slate-400 font-normal">(optional)</span></label><input [(ngModel)]="form.phone" name="phone" class="input" /></div>
              <div><label class="lbl">Subject</label><input [(ngModel)]="form.subject" name="subject" class="input" /></div>
              <div><label class="lbl">Message</label><textarea [(ngModel)]="form.body" name="body" rows="5" required class="input"></textarea></div>

              <!-- Honeypot: off-screen and aria-hidden, so only bots fill it. -->
              <input [(ngModel)]="form.website" name="website" tabindex="-1" autocomplete="off"
                     aria-hidden="true" class="absolute -left-[9999px] w-px h-px opacity-0" />

              @if (error()) { <p class="text-sm text-red-600">{{ error() }}</p> }
              <button type="submit" class="btn-primary" [disabled]="sending()">
                {{ sending() ? 'Sending…' : 'Send message' }}
              </button>
            </form>
          }
        </div>
      </div>
    </section>
  `,
})
export class ContactComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly theme = inject(ThemeService);
  private readonly contact = inject(ContactService);
  private readonly siteUrl = inject(SITE_URL);

  readonly store = computed(() => this.theme.storeName() || 'our store');
  readonly sent = signal(false);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  form = { name: '', email: '', phone: '', subject: '', body: '', website: '' };

  private static readonly BASE_DETAILS = [
    { icon: '💬', label: 'Response time', value: 'We usually reply within 1 business day.' },
    { icon: '🕒', label: 'Support hours', value: 'Mon–Sat, 9:30 AM – 6:30 PM' },
  ];
  private static readonly FALLBACK_EMAIL_DETAIL =
    { icon: '✉️', label: 'Prefer email?', value: 'Send us a message using the form and we will get back to you.' };

  readonly details = signal<{ icon: string; label: string; value: string }[]>(ContactComponent.BASE_DETAILS.concat(ContactComponent.FALLBACK_EMAIL_DETAIL));

  ngOnInit(): void {
    const name = this.theme.storeName() || 'our store';
    this.seo.setMeta({
      title: `Contact us — ${name}`,
      description: `Get in touch with ${name} for help with orders, products or any other questions.`,
      url: `${this.siteUrl}/contact`,
    });

    this.contact.getStoreContact().subscribe((c) => {
      const extra: { icon: string; label: string; value: string }[] = [];
      extra.push(c.storeEmail ? { icon: '✉️', label: 'Email us', value: c.storeEmail } : ContactComponent.FALLBACK_EMAIL_DETAIL);
      if (c.storePhone) extra.push({ icon: '📞', label: 'Call us', value: c.storePhone });
      if (c.storeAddress) extra.push({ icon: '📍', label: 'Visit us', value: c.storeAddress });
      this.details.set(ContactComponent.BASE_DETAILS.concat(extra));
    });
  }

  submit(): void {
    if (!this.form.name.trim() || !this.form.email.trim() || !this.form.body.trim()) return;

    this.sending.set(true);
    this.error.set(null);
    this.contact.submit({
      name: this.form.name,
      email: this.form.email,
      phone: this.form.phone || null,
      subject: this.form.subject || null,
      body: this.form.body,
      sourceUrl: typeof location !== 'undefined' ? location.href : null,
      website: this.form.website || null,
    }).subscribe({
      next: () => {
        this.sent.set(true);
        this.sending.set(false);
        this.form = { name: '', email: '', phone: '', subject: '', body: '', website: '' };
      },
      error: (e) => {
        this.error.set(e?.status === 429
          ? "You've sent a few messages already — please try again a little later."
          : e?.error?.message ?? "Sorry, that didn't send. Please try again.");
        this.sending.set(false);
      },
    });
  }
}
