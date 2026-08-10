import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { API_BASE_URL, SITE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { ContentPage } from '../../../core/models/content-page.model';
import { BrandingService } from '../../../core/services/branding.service';
import { SeoService } from '../../../core/services/seo.service';

@Component({
  selector: 'app-contact',
  imports: [FormsModule],
  template: `
    <section class="page-container py-12">
      <div class="max-w-2xl mb-10">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900">{{ page?.title || 'Contact us' }}</h1>
        @if (intro) {
          <div class="mt-3 text-slate-600" [innerHTML]="intro"></div>
        } @else {
          <p class="mt-3 text-slate-600">Questions about an order, customization, or a bulk enquiry? We are happy to help.</p>
        }
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

              <button type="submit" [disabled]="sending()" class="btn-primary">
                {{ sending() ? 'Sending…' : 'Send message' }}
              </button>
              @if (error(); as e) { <p class="text-sm text-red-600">{{ e }}</p> }
              <p class="text-xs text-slate-400">Leave an email address or a phone number so we can reply.</p>
            </form>
          }
        </div>
      </div>
    </section>
  `,
})
export class ContactComponent implements OnInit {
  private readonly seo = inject(SeoService);

  private readonly http = inject(HttpClient);

  readonly sent = signal(false);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);

  form = { name: '', email: '', phone: '', subject: '', message: '', website: '', subscribe: false };

  private readonly route = inject(ActivatedRoute);
  private readonly branding = inject(BrandingService);

  readonly page = this.route.snapshot.data['page'] as ContentPage | null;

  /** The editable blurb above the form (055). */
  readonly intro = (this.page?.sections ?? []).find((s) => s.sectionType === 'Prose')?.content ?? null;

  /**
   * From settings, not hardcoded. Only what has been filled in is shown — a contact block
   * listing "Email" with nothing beside it is worse than no line at all, and these were live
   * placeholders (support@calendarshop.example) until now.
   */
  readonly details = computed(() => {
    const c = this.branding.contact();
    return [
      { icon: '📍', label: 'Address', value: c.address },
      { icon: '📞', label: 'Phone', value: c.phone },
      { icon: '✉️', label: 'Email', value: c.email },
      { icon: '🕒', label: 'Hours', value: c.hours },
    ].filter((d) => d.value?.trim());
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
