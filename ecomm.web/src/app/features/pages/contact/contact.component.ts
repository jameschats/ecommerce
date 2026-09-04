import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { API_BASE_URL, SITE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { ContentPage } from '../../../core/models/content-page.model';
import { BrandingService } from '../../../core/services/branding.service';
import { SeoService } from '../../../core/services/seo.service';

/**
 * Same card language as /enquiry (rounded-2xl, shadow-sm, p-6 sm:p-8, centered header,
 * full-width primary button) — the two pages read as one family now instead of the plainer,
 * shadowless card this used to be. Contact details still come from BrandingService (settings,
 * not hardcoded), and the page title/intro stay CMS-editable exactly as before.
 */
@Component({
  selector: 'app-contact',
  imports: [FormsModule],
  template: `
    <section class="page-container py-10 sm:py-14">
      <div class="max-w-5xl mx-auto">
        <div class="max-w-2xl mx-auto text-center mb-8 sm:mb-10">
          <h1 class="text-2xl sm:text-3xl font-bold text-slate-900">
            {{ page?.title || ('Get in touch with ' + shopName()) }}
          </h1>
          @if (intro) {
            <div class="mt-3 text-slate-600 intro-copy" [innerHTML]="intro"></div>
          } @else {
            <p class="mt-3 text-slate-600">Questions about an order, customization, or a bulk enquiry? We are happy to help.</p>
          }
        </div>

        <!--
          Five-column split only when there is a details card to fill — with contact details
          unset the form alone stays centered at a readable width instead of stretching into
          empty space.
        -->
        <div class="grid gap-6 lg:gap-8" [class]="details().length ? 'lg:grid-cols-5' : ''">
          @if (details().length) {
            <div class="lg:col-span-2 rounded-2xl border border-slate-200 bg-white p-6 sm:p-8 shadow-sm h-fit">
              <h2 class="text-lg font-bold text-slate-900 mb-5">Contact details</h2>
              <div class="space-y-5">
                @for (d of details(); track d.label) {
                  <div class="flex items-start gap-3">
                    <span class="shrink-0 grid place-items-center w-9 h-9 rounded-full bg-primary/10 text-primary">{{ d.icon }}</span>
                    <div class="min-w-0">
                      <div class="text-sm font-semibold text-slate-800">{{ d.label }}</div>
                      @for (line of d.lines; track line) {
                        @if (d.href) {
                          <a [href]="d.href(line)" class="block text-sm text-primary hover:underline break-words">{{ line }}</a>
                        } @else {
                          <div class="text-sm text-slate-500 whitespace-pre-line break-words">{{ line }}</div>
                        }
                      }
                    </div>
                  </div>
                }
              </div>

              @if (whatsappHref() || phoneHref() || emailHref()) {
                <div class="flex flex-wrap gap-2 mt-6 pt-6 border-t border-slate-100">
                  @if (whatsappHref()) {
                    <a [href]="whatsappHref()" target="_blank" rel="noopener"
                       class="inline-flex items-center gap-1.5 text-sm font-medium bg-emerald-50 text-emerald-700 border border-emerald-200 rounded-lg px-3 py-1.5 hover:bg-emerald-100 transition">
                      💬 WhatsApp
                    </a>
                  }
                  @if (phoneHref()) {
                    <a [href]="phoneHref()"
                       class="inline-flex items-center gap-1.5 text-sm font-medium bg-slate-50 text-slate-700 border border-slate-200 rounded-lg px-3 py-1.5 hover:bg-slate-100 transition">
                      📞 Call
                    </a>
                  }
                  @if (emailHref()) {
                    <a [href]="emailHref()"
                       class="inline-flex items-center gap-1.5 text-sm font-medium bg-slate-50 text-slate-700 border border-slate-200 rounded-lg px-3 py-1.5 hover:bg-slate-100 transition">
                      ✉️ Email
                    </a>
                  }
                </div>
              }
            </div>
          }

          <!-- Form -->
          <div class="rounded-2xl border border-slate-200 bg-white p-6 sm:p-8 shadow-sm"
               [class]="details().length ? 'lg:col-span-3' : 'max-w-2xl mx-auto w-full'">
            @if (sent()) {
              <div class="text-center py-10">
                <div class="text-4xl">✅</div>
                <h2 class="text-lg font-semibold text-slate-800 mt-3">Thanks for reaching out!</h2>
                <p class="text-slate-500 text-sm mt-1">We'll get back to you within 1 business day.</p>
                <button type="button" (click)="sent.set(false)" class="btn-ghost mt-5 border border-slate-300">Send another message</button>
              </div>
            } @else {
              <h2 class="text-lg font-bold text-slate-900 mb-5">Send us a message</h2>
              <form (ngSubmit)="submit()" class="space-y-4">
                <div class="grid sm:grid-cols-2 gap-4">
                  <div><label class="lbl">Name</label><input [(ngModel)]="form.name" name="name" required class="input" /></div>
                  <div><label class="lbl">Email</label><input [(ngModel)]="form.email" name="email" type="email" class="input" /></div>
                </div>
                <div><label class="lbl">Phone</label><input [(ngModel)]="form.phone" name="phone" type="tel" class="input" placeholder="So we can call you back" /></div>
                <div><label class="lbl">Subject</label><input [(ngModel)]="form.subject" name="subject" class="input" /></div>
                <div><label class="lbl">Message</label><textarea [(ngModel)]="form.message" name="message" rows="5" required class="input"></textarea></div>

                <!--
                  Honeypot. Hidden from people and skipped by the tab order, so anything in it
                  came from a bot filling every field it found. aria-hidden keeps it out of a
                  screen reader too — it is not a real question.
                -->
                <input [(ngModel)]="form.website" name="website" tabindex="-1" autocomplete="off"
                       aria-hidden="true" class="hidden" />

                <label class="flex items-start gap-2 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="form.subscribe" name="subscribe" class="w-4 h-4 mt-0.5" />
                  <span class="text-sm text-slate-600">Email me about offers and new designs</span>
                </label>

                <button type="submit" [disabled]="sending()" class="btn-primary w-full py-3 text-base">
                  {{ sending() ? 'Sending…' : 'Send message' }}
                </button>
                @if (error(); as e) { <p class="text-sm text-red-600">{{ e }}</p> }
                <p class="text-xs text-slate-400 text-center">Leave an email address or a phone number so we can reply.</p>
              </form>
            }
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    /* The editor emits bare tags and Tailwind's reset strips their margins, so paragraphs
       and lists would otherwise run together as one block of text. */
    .intro-copy :is(p, ul, ol) { margin-block: 0.6rem; }
    .intro-copy :is(ul, ol) { padding-inline-start: 1.4rem; }
    .intro-copy ul { list-style: disc; }
    .intro-copy ol { list-style: decimal; }
    .intro-copy li { margin-block: 0.25rem; }
    .intro-copy :is(h3, h4) { font-weight: 600; margin-block: 0.8rem 0.3rem; color: rgb(30 41 59); }
    .intro-copy a { color: var(--color-primary, #2563eb); text-decoration: underline; }
    .intro-copy strong { font-weight: 600; color: rgb(30 41 59); }
  `],
})
export class ContactComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly branding = inject(BrandingService);

  readonly sent = signal(false);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);

  form = { name: '', email: '', phone: '', subject: '', message: '', website: '', subscribe: false };

  readonly page = this.route.snapshot.data['page'] as ContentPage | null;

  /** The editable blurb above the form (055). */
  readonly intro = (this.page?.sections ?? []).find((s) => s.sectionType === 'Prose')?.content ?? null;

  /** Falls back to a neutral word, so the heading never reads "with" and stops. */
  shopName(): string {
    return `${this.branding.siteName()}${this.branding.siteNameAccent()}`.trim() || 'us';
  }

  /**
   * From settings, not hardcoded. Only what has been filled in is shown — a contact block
   * listing "Email" with nothing beside it is worse than no line at all, and these were live
   * placeholders (support@calendarshop.example) until now.
   *
   * Mobile/Landline each collapse up to two configured numbers into one row rather than
   * showing four near-identical lines — every number stays individually tappable, just
   * grouped under the one label.
   */
  readonly details = computed(() => {
    const c = this.branding.contact();
    const tel = (v: string) => `tel:${v.replace(/[^\d+]/g, '')}`;
    const numbers = (...vs: string[]) => vs.map((v) => v?.trim()).filter((v): v is string => !!v);

    return [
      { icon: '📍', label: 'Address', lines: numbers(c.address), href: undefined as ((v: string) => string) | undefined },
      { icon: '📱', label: 'Mobile', lines: numbers(c.mobile1, c.mobile2), href: tel },
      { icon: '☎️', label: 'Landline', lines: numbers(c.landline1, c.landline2), href: tel },
      { icon: '✉️', label: 'Email', lines: numbers(c.email), href: (v: string) => `mailto:${v}` },
      { icon: '🕒', label: 'Hours', lines: numbers(c.hours), href: undefined },
    ].filter((d) => d.lines.length);
  });

  /** tel:/mailto:/wa.me quick actions — built from the same phone/email settings, no separate config. */
  readonly phoneHref = computed(() => {
    const c = this.branding.contact();
    const first = [c.mobile1, c.mobile2, c.landline1, c.landline2].map((v) => v?.trim()).find((v) => v);
    return first ? `tel:${first.replace(/[^\d+]/g, '')}` : null;
  });

  readonly emailHref = computed(() => {
    const email = this.branding.contact().email.trim();
    return email ? `mailto:${email}` : null;
  });

  /** Landlines can't take WhatsApp, so this only ever offers a mobile number. */
  readonly whatsappHref = computed(() => {
    const c = this.branding.contact();
    const mobile = [c.mobile1, c.mobile2].map((v) => v?.trim()).find((v) => v);
    if (!mobile) return null;
    const digits = mobile.replace(/\D/g, '');
    if (!digits) return null;
    // A bare 10-digit Indian mobile number needs the country code for wa.me to resolve it;
    // anything longer is assumed to already carry one.
    const withCountryCode = digits.length === 10 ? `91${digits}` : digits;
    return `https://wa.me/${withCountryCode}`;
  });

  ngOnInit(): void {
    this.seo.setMeta({
      title: this.page?.metaTitle || 'Contact us',
      description: this.page?.metaDescription
        ?? 'Get in touch about orders, customization help, or bulk and corporate calendar enquiries.',
      url: `${SITE_URL}/contact`,
    });
  }

  /**
   * Sends the enquiry. This used to set a "thanks" message and clear the boxes without
   * telling anyone — every message sent through this page was lost.
   */
  submit(): void {
    this.error.set(null);
    if (!this.form.name.trim() || !this.form.message.trim()) {
      this.error.set('Please enter your name and a message.');
      return;
    }
    if (!this.form.email.trim() && !this.form.phone.trim()) {
      this.error.set('Leave an email address or a phone number so we can reply.');
      return;
    }

    this.sending.set(true);
    this.http
      .post<ApiResponse<unknown>>(`${API_BASE_URL}/contact`, {
        name: this.form.name.trim(),
        email: this.form.email.trim() || null,
        phone: this.form.phone.trim() || null,
        subject: this.form.subject.trim() || null,
        message: this.form.message.trim(),
        sourcePage: '/contact',
        website: this.form.website,
        subscribeToEmails: this.form.subscribe,
      })
      .subscribe({
        next: () => {
          this.sending.set(false);
          this.sent.set(true);
          this.form = { name: '', email: '', phone: '', subject: '', message: '', website: '', subscribe: false };
        },
        error: (e) => {
          this.sending.set(false);
          this.error.set(e?.error?.message ?? 'Could not send your message. Please try again.');
        },
      });
  }
}
