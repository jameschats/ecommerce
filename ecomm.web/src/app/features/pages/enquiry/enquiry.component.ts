import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL, SITE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { BrandingService } from '../../../core/services/branding.service';
import { SeoService } from '../../../core/services/seo.service';

/**
 * Bulk-requirement enquiries — a lead form, not the general contact form.
 *
 * Deliberately separate from /contact: that one asks what your question is, this one asks
 * what you want quoted. It files into the same inbox with source "Enquiry", so leads can be
 * told apart from questions without a second place to look.
 */
@Component({
  selector: 'app-enquiry',
  imports: [FormsModule],
  template: `
    <section class="page-container py-10 sm:py-14">
      <div class="max-w-2xl mx-auto">

        <h1 class="text-2xl sm:text-3xl font-bold text-center text-slate-900">
          Get a price quote from <span class="text-primary">{{ shopName() }}</span>
        </h1>
        <p class="mt-3 text-center text-slate-600">
          Tell us what you need and we will send you wholesale rates.
        </p>

        <div class="mt-8 rounded-2xl border border-slate-200 bg-white p-6 sm:p-8 shadow-sm">
          @if (sent()) {
            <div class="text-center py-10">
              <div class="text-4xl">✅</div>
              <h2 class="text-lg font-semibold text-slate-800 mt-3">Thanks — we have your requirement</h2>
              <p class="text-slate-500 text-sm mt-1">We will get back to you with a quote shortly.</p>
              <button type="button" (click)="reset()" class="btn-ghost mt-5 border border-slate-300">Send another enquiry</button>
            </div>
          } @else {
            <h2 class="text-lg font-bold text-slate-900 mb-5">Request a quote</h2>

            <form (ngSubmit)="submit()" class="space-y-4">
              <div class="grid sm:grid-cols-2 gap-4">
                <div>
                  <label class="lbl">Name <span class="text-red-500">*</span></label>
                  <input [(ngModel)]="form.name" name="name" class="input" placeholder="Your name" />
                </div>
                <div>
                  <label class="lbl">Phone <span class="text-red-500">*</span></label>
                  <input [(ngModel)]="form.phone" name="phone" type="tel" class="input" placeholder="Mobile number" />
                </div>
              </div>

              <div class="grid sm:grid-cols-2 gap-4">
                <div>
                  <label class="lbl">Email</label>
                  <input [(ngModel)]="form.email" name="email" type="email" class="input" placeholder="name@example.com" />
                </div>
                <div>
                  <label class="lbl">Design number</label>
                  <input [(ngModel)]="form.designNo" name="designNo" class="input" placeholder="If you know it" />
                </div>
              </div>

              <div>
                <label class="lbl">What do you need? <span class="text-red-500">*</span></label>
                <select [(ngModel)]="form.requirement" name="requirement" class="input">
                  <option value="">Choose an option</option>
                  @for (o of requirements; track o) { <option [value]="o">{{ o }}</option> }
                </select>
              </div>

              <div>
                <label class="lbl">Give us more details <span class="text-red-500">*</span></label>
                <textarea [(ngModel)]="form.details" name="details" rows="5" class="input"
                          placeholder="Quantities, sizes, paper, branding, delivery date…"></textarea>
              </div>

              <!-- Honeypot, same as the contact form: hidden from people and skipped by the
                   tab order, so anything in it came from a bot filling every field it found. -->
              <input [(ngModel)]="form.website" name="website" tabindex="-1" autocomplete="off"
                     aria-hidden="true" class="hidden" />

              <button type="submit" [disabled]="sending()" class="btn-primary w-full py-3 text-base">
                {{ sending() ? 'Sending…' : 'Request a quote' }}
              </button>

              @if (error(); as e) { <p class="text-sm text-red-600">{{ e }}</p> }
              <p class="text-xs text-slate-400 text-center">
                We reply by phone or email — leave whichever suits you.
              </p>
            </form>
          }
        </div>
      </div>
    </section>
  `,
})
export class EnquiryComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  private readonly branding = inject(BrandingService);

  readonly sent = signal(false);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);

  /** Falls back to a neutral word, so the heading never reads "quote from" and stops. */
  shopName(): string {
    return `${this.branding.siteName()}${this.branding.siteNameAccent()}`.trim() || 'us';
  }

  /**
   * The buying decisions this shop actually sells against. Fixed rather than admin-editable
   * for now — a handful of options are not worth a settings screen until the list is argued about.
   */
  readonly requirements = [
    'Finished calendars (ready to hang)',
    'Daily calendar mount and other items',
    'Bulk requirements',
    'Design number quote',
    'Custom branding or corporate gifting',
    'Sample request',
    'Something else',
  ];

  form = { name: '', phone: '', email: '', designNo: '', requirement: '', details: '', website: '' };

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'Bulk enquiry — request a quote',
      description: 'Send us your calendar requirement — quantities, sizes and branding — and we will reply with wholesale rates.',
      url: `${SITE_URL}/enquiry`,
    });
  }

  reset(): void {
    this.form = { name: '', phone: '', email: '', designNo: '', requirement: '', details: '', website: '' };
    this.sent.set(false);
    this.error.set(null);
  }

  submit(): void {
    this.error.set(null);

    // Checked here as well as on the server, so a mistake is caught while the field is still
    // in front of the person rather than after a round trip.
    if (!this.form.name.trim()) { this.error.set('Please tell us your name.'); return; }
    if (!this.form.phone.trim() && !this.form.email.trim()) {
      this.error.set('Leave a phone number or an email address so we can reply.');
      return;
    }
    if (!this.form.requirement) { this.error.set('Please choose what you need.'); return; }
    if (!this.form.details.trim()) { this.error.set('Please tell us a little about your requirement.'); return; }

    this.sending.set(true);
    this.http.post<ApiResponse<unknown>>(`${API_BASE_URL}/contact/enquiry`, {
      name: this.form.name.trim(),
      phone: this.form.phone.trim() || null,
      email: this.form.email.trim() || null,
      designNo: this.form.designNo.trim() || null,
      requirement: this.form.requirement,
      details: this.form.details.trim(),
      sourcePage: '/enquiry',
      website: this.form.website,
    }).subscribe({
      next: () => { this.sending.set(false); this.sent.set(true); },
      error: (e) => {
        this.sending.set(false);
        this.error.set(e?.error?.message ?? 'Could not send your enquiry. Please try again.');
      },
    });
  }
}
