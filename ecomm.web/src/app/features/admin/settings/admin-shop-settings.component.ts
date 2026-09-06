import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface StateMinOrderRow {
  id: number | null;
  stateName: string;
  minOrderAmount: number;
  isActive: boolean;
}

interface ShopSettings {
  minOrderAmount: number;
  packingChargePct: number;
  roundOffEnabled: boolean;
  announcementText: string;
  priceValidUpto: string;
  upiId: string;
  upiPayeeName: string;
  bankAccountName: string;
  bankAccountNumber: string;
  bankIfsc: string;
  bankName: string;
  emailMode: string;
  smtpHost: string;
  smtpPort: number;
  smtpUsername: string;
  /** The password itself is never sent to the browser — only whether one is stored. */
  smtpPasswordSet: boolean;
  smtpPassword?: string;
  fromAddress: string;
  fromName: string;
  adminNotifyTo: string;
  browserTitle: string;
  faviconUrl: string;
  siteName: string;
  siteNameAccent: string;
  siteNameSize: string;
  logoUrl: string;
  footerLogoUrl: string;
  footerDescription: string;
  contactAddress: string;
  contactMobile1: string;
  contactMobile2: string;
  contactLandline1: string;
  contactLandline2: string;
  contactEmail: string;
  contactHours: string;
  contactCity: string;
  socialFacebookUrl: string;
  socialFacebookEnabled: boolean;
  socialInstagramUrl: string;
  socialInstagramEnabled: boolean;
  socialXUrl: string;
  socialXEnabled: boolean;
  socialLinkedinUrl: string;
  socialLinkedinEnabled: boolean;
  stateMinOrders: StateMinOrderRow[];
}

/**
 * Shop & payment settings (design.md §10.3).
 *
 * These drive the customer-facing payment page and the order form's totals, so they live
 * in the database and are edited here rather than in a config file on the server.
 */
@Component({
  selector: 'app-admin-shop-settings',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Wider than the 5xl the list pages use, because the body is a two-column grid. -->
    <div class="max-w-6xl mx-auto p-6">
    <h1 class="text-xl font-bold text-slate-900">Shop &amp; payment settings</h1>
    <p class="text-sm text-slate-500 mt-0.5">
      Drives the order form totals and the customer payment page.
    </p>

    @if (loading()) {
      <div class="mt-4 h-64 rounded-xl bg-slate-100 animate-pulse"></div>
    } @else if (model(); as m) {
      <div class="mt-5 grid lg:grid-cols-2 gap-5">

        <!-- ------------------------------ payment ------------------------------ -->
        <section class="rounded-xl border border-slate-200 bg-white p-5">
          <h2 class="font-semibold text-slate-900">Payment details</h2>
          <p class="text-sm text-slate-500 mt-0.5">
            Shown to the customer after they place an order. The UPI QR is built from these.
          </p>

          @if (!m.upiId && !m.bankAccountNumber) {
            <p class="mt-3 text-sm rounded-lg bg-amber-50 border border-amber-200 px-3 py-2 text-amber-900">
              No payment details set — customers currently see a "not configured" message.
            </p>
          }

          <label class="block mt-4">
            <span class="form-label">UPI ID</span>
            <input class="form-input" [(ngModel)]="m.upiId" placeholder="yourname@okicici" />
          </label>
          <label class="block mt-3">
            <span class="form-label">UPI payee name</span>
            <input class="form-input" [(ngModel)]="m.upiPayeeName" placeholder="Shown in the payer's app" />
          </label>

          <hr class="my-4 border-slate-100" />

          <label class="block">
            <span class="form-label">Bank account name</span>
            <input class="form-input" [(ngModel)]="m.bankAccountName" />
          </label>
          <label class="block mt-3">
            <span class="form-label">Account number</span>
            <input class="form-input font-mono" [(ngModel)]="m.bankAccountNumber" />
          </label>
          <div class="grid sm:grid-cols-2 gap-3 mt-3">
            <label class="block">
              <span class="form-label">IFSC</span>
              <input class="form-input font-mono uppercase" [(ngModel)]="m.bankIfsc" />
            </label>
            <label class="block">
              <span class="form-label">Bank name</span>
              <input class="form-input" [(ngModel)]="m.bankName" />
            </label>
          </div>
        </section>

        <!-- ------------------------------ order rules ------------------------------ -->
        <section class="rounded-xl border border-slate-200 bg-white p-5">
          <h2 class="font-semibold text-slate-900">Order rules</h2>
          <p class="text-sm text-slate-500 mt-0.5">Applied when the order form prices a basket.</p>

          <div class="grid sm:grid-cols-2 gap-3 mt-4">
            <label class="block">
              <span class="form-label">Minimum order (₹)</span>
              <input type="number" min="0" step="1" class="form-input" [(ngModel)]="m.minOrderAmount" />
              <span class="text-xs text-slate-500 mt-1 block">0 = no minimum. Hidden from customers when 0.</span>
            </label>
            <label class="block">
              <span class="form-label">Packing charge (%)</span>
              <input type="number" min="0" max="100" step="0.01" class="form-input" [(ngModel)]="m.packingChargePct" />
            </label>
          </div>

          <label class="flex items-center gap-2 mt-3">
            <input type="checkbox" [(ngModel)]="m.roundOffEnabled" class="w-4 h-4" />
            <span class="text-sm text-slate-700">Round the overall amount to the nearest rupee</span>
          </label>

          <hr class="my-4 border-slate-100" />

          <label class="block">
            <span class="form-label">Announcement text</span>
            <input class="form-input" [(ngModel)]="m.announcementText"
                   placeholder="e.g. Diwali booking open — prices valid to 31 July" />
          </label>
          <label class="block mt-3">
            <span class="form-label">Prices valid up to</span>
            <input class="form-input" [(ngModel)]="m.priceValidUpto" placeholder="e.g. 31 July 2026" />
          </label>
        </section>

        <!-- ------------------------------ site identity ------------------------------ -->
        <section class="rounded-xl border border-slate-200 bg-white p-5 lg:col-span-2">
          <h2 class="font-semibold text-slate-900">Site identity</h2>
          <p class="text-sm text-slate-500 mt-0.5">
            The name and logo in the storefront header and footer, and what the browser tab shows.
            Leave any of these blank to keep the built-in defaults.
          </p>

          <div class="grid sm:grid-cols-2 gap-4 mt-4">
            <label class="block">
              <span class="form-label">Site name</span>
              <div class="flex items-center gap-2">
                <input class="form-input flex-1" [(ngModel)]="m.siteName" placeholder="e.g. Calendar" />
                <input class="form-input flex-1" [(ngModel)]="m.siteNameAccent" placeholder="Highlighted, e.g. Shop" />
              </div>
              <!-- Live preview: the two boxes join with no space, the second takes the theme's
                   primary colour, and it renders at the chosen size — easier to trust than a
                   sentence describing it. -->
              @if (m.siteName || m.siteNameAccent) {
                <span class="mt-2 block font-bold text-xl" [style.fontSize]="m.siteNameSize ? m.siteNameSize + 'rem' : null">
                  {{ m.siteName }}<span class="text-primary">{{ m.siteNameAccent }}</span>
                </span>
              }
              <span class="text-xs text-slate-500 mt-1 block">
                Shown beside the logo in the header, in the footer and in the copyright line.
                The second box is joined on with no space and drawn in your primary colour —
                leave it blank for a single-colour name.
              </span>

              <!-- Size of the wordmark only, not the rest of the site. Kept here rather than
                   on the Theme page because it is part of the name, and you want to see it
                   against the name you just typed. -->
              <span class="form-label mt-4 block">Name size</span>
              <div class="flex flex-wrap gap-2">
                @for (s of nameSizes; track s.value) {
                  <button type="button" (click)="m.siteNameSize = s.value"
                          class="px-3 py-1.5 rounded-lg border text-sm transition"
                          [class]="(m.siteNameSize || '1.25') === s.value
                            ? 'border-primary bg-primary/5 text-slate-900 font-medium'
                            : 'border-slate-200 text-slate-600 hover:border-slate-300'">
                    {{ s.label }}
                  </button>
                }
              </div>
              <span class="text-xs text-slate-500 mt-1 block">
                Applies to the name in the header and footer only.
              </span>
            </label>

            <div>
              <span class="form-label">Header logo</span>
              <div class="flex items-center gap-3">
                <span class="w-16 h-10 shrink-0 rounded border border-slate-200 bg-slate-50 grid place-items-center overflow-hidden">
                  @if (m.logoUrl) {
                    <img [src]="m.logoUrl" alt="Logo preview" class="max-w-full max-h-full object-contain" />
                  } @else {
                    <span class="text-slate-300 text-xs">none</span>
                  }
                </span>
                <input class="form-input flex-1" [(ngModel)]="m.logoUrl" placeholder="Paste a URL, or upload →" />
                <label class="shrink-0 cursor-pointer bg-slate-800 hover:bg-slate-900 text-white text-sm
                              font-medium px-4 py-2.5 rounded-lg transition">
                  {{ uploading() === 'logoUrl' ? 'Uploading…' : 'Upload' }}
                  <input type="file" accept="image/png,image/jpeg,image/webp,image/gif"
                         class="hidden" (change)="uploadImage($event, m, 'logoUrl')" />
                </label>
              </div>
              <span class="text-xs text-slate-500 mt-1 block">
                Sits before the name on the white header. Rendered 36px tall, so a wide
                transparent PNG works best. Set a logo and leave the name blank to show the
                logo on its own.
              </span>
            </div>

            <div>
              <span class="form-label">Footer logo</span>
              <!-- Previewed on the real footer colour. A mark that looks right on the white
                   swatch above can be invisible here, which is the whole reason this is a
                   separate setting. -->
              <div class="flex items-center gap-3">
                <span class="w-16 h-10 shrink-0 rounded border border-slate-700 bg-slate-900 grid place-items-center overflow-hidden">
                  @if (m.footerLogoUrl || m.logoUrl) {
                    <img [src]="m.footerLogoUrl || m.logoUrl" alt="Footer logo preview" class="max-w-full max-h-full object-contain" />
                  } @else {
                    <span class="text-slate-500 text-xs">none</span>
                  }
                </span>
                <input class="form-input flex-1" [(ngModel)]="m.footerLogoUrl" placeholder="Blank = reuse header logo" />
                <label class="shrink-0 cursor-pointer bg-slate-800 hover:bg-slate-900 text-white text-sm
                              font-medium px-4 py-2.5 rounded-lg transition">
                  {{ uploading() === 'footerLogoUrl' ? 'Uploading…' : 'Upload' }}
                  <input type="file" accept="image/png,image/jpeg,image/webp,image/gif"
                         class="hidden" (change)="uploadImage($event, m, 'footerLogoUrl')" />
                </label>
              </div>
              <span class="text-xs text-slate-500 mt-1 block">
                The footer is dark, so a light or white version of the mark usually reads
                best. Leave blank to reuse the header logo.
              </span>
            </div>

            <label class="block">
              <span class="form-label">Footer description</span>
              <textarea class="form-input" rows="6" [(ngModel)]="m.footerDescription"
                        placeholder="Custom 2026 calendars — wall, desk, pocket & more. Personalized with your photos, brand name and logo."></textarea>
              <span class="text-xs text-slate-500 mt-1 block">
                Shown under the logo in the footer. Leave a blank line between paragraphs to
                split them, and any web address typed in (e.g. dailycalendarstore.in) becomes
                a clickable link automatically. Leave blank to use the built-in default line.
              </span>
            </label>

            <!--
              One place for how to reach the shop. The contact page, the footer and the
              structured data crawlers read all take these — the site used to say Chennai on
              the contact page while its structured data said Madurai.
            -->
            <div class="sm:col-span-2 border-t border-slate-200 pt-4 mt-2">
              <h3 class="font-semibold text-slate-800 mb-1">Contact details</h3>
              <p class="text-xs text-slate-500 mb-3">
                Shown on the contact page and read by search engines and AI assistants.
                Anything left blank is simply not shown.
              </p>
              <div class="grid gap-3 sm:grid-cols-2">
                <label class="block sm:col-span-2">
                  <span class="form-label">Address</span>
                  <textarea class="form-input" rows="2" [(ngModel)]="m.contactAddress"
                            placeholder="Shop name&#10;Street, Area"></textarea>
                </label>
                <label class="block">
                  <span class="form-label">City</span>
                  <input class="form-input" [(ngModel)]="m.contactCity" placeholder="e.g. Madurai" />
                </label>
                <label class="block">
                  <span class="form-label">Email</span>
                  <input class="form-input" type="email" [(ngModel)]="m.contactEmail" placeholder="e.g. orders@yourshop.com" />
                </label>
                <label class="block">
                  <span class="form-label">Opening hours</span>
                  <input class="form-input" [(ngModel)]="m.contactHours" placeholder="e.g. Mon–Sat, 9:30 AM – 6:30 PM" />
                </label>
              </div>

              <div class="grid gap-3 sm:grid-cols-2 mt-3">
                <label class="block">
                  <span class="form-label">Mobile 1</span>
                  <input class="form-input" [(ngModel)]="m.contactMobile1" placeholder="e.g. +91 98765 43210" />
                </label>
                <label class="block">
                  <span class="form-label">Mobile 2 <span class="text-slate-400 font-normal">(optional)</span></span>
                  <input class="form-input" [(ngModel)]="m.contactMobile2" placeholder="e.g. +91 98765 43211" />
                </label>
                <label class="block">
                  <span class="form-label">Landline 1 <span class="text-slate-400 font-normal">(optional)</span></span>
                  <input class="form-input" [(ngModel)]="m.contactLandline1" placeholder="e.g. 04562 123456" />
                </label>
                <label class="block">
                  <span class="form-label">Landline 2 <span class="text-slate-400 font-normal">(optional)</span></span>
                  <input class="form-input" [(ngModel)]="m.contactLandline2" placeholder="e.g. 04562 123457" />
                </label>
              </div>
              <p class="text-xs text-slate-400 mt-2">The WhatsApp button on the contact page uses Mobile 1 (falling back to Mobile 2).</p>
            </div>

            <div class="sm:col-span-2 border-t border-slate-200 pt-4 mt-2">
              <h3 class="font-semibold text-slate-800 mb-1">Social links</h3>
              <p class="text-xs text-slate-500 mb-3">
                Icons shown in the footer. Each is switched on independently, so a profile
                you haven't set up yet just stays hidden instead of linking nowhere.
              </p>
              <div class="grid gap-3 sm:grid-cols-2">
                <label class="block">
                  <span class="form-label">Facebook</span>
                  <div class="flex items-center gap-2">
                    <input class="form-input flex-1" [(ngModel)]="m.socialFacebookUrl" placeholder="https://facebook.com/yourshop" />
                    <input type="checkbox" [(ngModel)]="m.socialFacebookEnabled" title="Show in footer" />
                  </div>
                </label>
                <label class="block">
                  <span class="form-label">Instagram</span>
                  <div class="flex items-center gap-2">
                    <input class="form-input flex-1" [(ngModel)]="m.socialInstagramUrl" placeholder="https://instagram.com/yourshop" />
                    <input type="checkbox" [(ngModel)]="m.socialInstagramEnabled" title="Show in footer" />
                  </div>
                </label>
                <label class="block">
                  <span class="form-label">X (Twitter)</span>
                  <div class="flex items-center gap-2">
                    <input class="form-input flex-1" [(ngModel)]="m.socialXUrl" placeholder="https://x.com/yourshop" />
                    <input type="checkbox" [(ngModel)]="m.socialXEnabled" title="Show in footer" />
                  </div>
                </label>
                <label class="block">
                  <span class="form-label">LinkedIn</span>
                  <div class="flex items-center gap-2">
                    <input class="form-input flex-1" [(ngModel)]="m.socialLinkedinUrl" placeholder="https://linkedin.com/company/yourshop" />
                    <input type="checkbox" [(ngModel)]="m.socialLinkedinEnabled" title="Show in footer" />
                  </div>
                </label>
              </div>
              <p class="text-xs text-slate-400 mt-2">Tick the box to show that icon in the footer. Leave unticked to hide it.</p>
            </div>

            <label class="block">
              <span class="form-label">Tab title</span>
              <input class="form-input" [(ngModel)]="m.browserTitle" placeholder="e.g. DailyCalendarShop" />
            </label>

            <div>
              <span class="form-label">Favicon</span>
              <div class="flex items-center gap-3">
                <span class="w-10 h-10 shrink-0 rounded border border-slate-200 bg-slate-50 grid place-items-center overflow-hidden">
                  @if (m.faviconUrl) {
                    <img [src]="m.faviconUrl" alt="Favicon preview" class="w-8 h-8 object-contain" />
                  } @else {
                    <span class="text-slate-300 text-xs">none</span>
                  }
                </span>
                <input class="form-input flex-1" [(ngModel)]="m.faviconUrl" placeholder="Paste a URL, or upload →" />
                <label class="shrink-0 cursor-pointer bg-slate-800 hover:bg-slate-900 text-white text-sm
                              font-medium px-4 py-2.5 rounded-lg transition">
                  {{ uploading() === 'faviconUrl' ? 'Uploading…' : 'Upload' }}
                  <input type="file" accept="image/png,image/jpeg,image/webp,image/gif"
                         class="hidden" (change)="uploadImage($event, m, 'faviconUrl')" />
                </label>
              </div>
              <span class="text-xs text-slate-500 mt-1 block">
                A square PNG works best — 32×32 or 64×64. Changes appear after a refresh;
                browsers cache favicons aggressively, so use a hard refresh if it looks stale.
              </span>
            </div>
          </div>

          <p class="text-xs text-slate-500 mt-3">
            The storefront caches branding for up to a minute, so give it a refresh after saving.
          </p>
          @if (uploadError()) { <span class="text-xs text-red-600 mt-1 block">{{ uploadError() }}</span> }
        </section>

        <!-- ------------------------------ email (Brevo) ------------------------------ -->
        <section class="rounded-xl border border-slate-200 bg-white p-5 lg:col-span-2">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 class="font-semibold text-slate-900">Email (Brevo SMTP)</h2>
              <p class="text-sm text-slate-500 mt-0.5">
                In <strong>Mock</strong> nothing is sent — messages are written to the server log.
                Switch to <strong>Live</strong> once the details below are correct.
              </p>
            </div>
            <div class="flex rounded-lg border border-slate-200 p-0.5 bg-slate-50 shrink-0">
              <button type="button" (click)="m.emailMode = 'Mock'"
                      [class]="m.emailMode !== 'Live' ? 'mode-on' : 'mode-off'">Mock</button>
              <button type="button" (click)="m.emailMode = 'Live'"
                      [class]="m.emailMode === 'Live' ? 'mode-on-live' : 'mode-off'">Live</button>
            </div>
          </div>

          @if (m.emailMode === 'Live' && (!m.smtpHost || !m.fromAddress || !m.smtpPasswordSet)) {
            <p class="mt-3 text-sm rounded-lg bg-amber-50 border border-amber-200 px-3 py-2 text-amber-900">
              Live mode needs an SMTP host, an SMTP key and a verified from-address. Until then,
              emails are logged and not sent.
            </p>
          }

          <div class="grid sm:grid-cols-2 gap-3 mt-4">
            <label class="block">
              <span class="form-label">SMTP host</span>
              <input class="form-input font-mono" [(ngModel)]="m.smtpHost" placeholder="smtp-relay.brevo.com" />
            </label>
            <label class="block">
              <span class="form-label">Port</span>
              <input type="number" class="form-input" [(ngModel)]="m.smtpPort" placeholder="587" />
            </label>
            <label class="block">
              <span class="form-label">SMTP login</span>
              <input class="form-input font-mono" [(ngModel)]="m.smtpUsername"
                     placeholder="from Brevo → SMTP & API → SMTP" />
            </label>
            <label class="block">
              <span class="form-label">
                SMTP key
                @if (m.smtpPasswordSet) { <span class="text-emerald-600 font-normal">· one is saved</span> }
              </span>
              <input type="password" class="form-input" [(ngModel)]="m.smtpPassword"
                     [placeholder]="m.smtpPasswordSet ? 'Leave blank to keep the saved key' : 'Paste your Brevo SMTP key'" />
            </label>
            <label class="block">
              <span class="form-label">From address</span>
              <input class="form-input" [(ngModel)]="m.fromAddress" placeholder="orders@yourdomain.in" />
              <span class="text-xs text-slate-500 mt-1 block">Must be a sender you have verified in Brevo.</span>
            </label>
            <label class="block">
              <span class="form-label">From name</span>
              <input class="form-input" [(ngModel)]="m.fromName" />
            </label>
            <label class="block sm:col-span-2">
              <span class="form-label">Send new-order alerts to</span>
              <input class="form-input" [(ngModel)]="m.adminNotifyTo" placeholder="your@email.com" />
            </label>
          </div>

          <div class="mt-4 pt-4 border-t border-slate-100 flex flex-wrap items-center gap-2">
            <input class="form-input flex-1 min-w-[220px]" [(ngModel)]="testTo" placeholder="Send a test email to…" />
            <button type="button" (click)="sendTest()" [disabled]="testing() || !testTo().includes('@')"
                    class="bg-slate-800 hover:bg-slate-900 disabled:bg-slate-300 text-white font-medium px-4 py-2.5 rounded-lg transition">
              {{ testing() ? 'Sending…' : 'Send test email' }}
            </button>
            <span class="text-xs text-slate-500 w-full">Save your changes first — the test uses the saved settings.</span>
            @if (testResult()) {
              <p class="w-full text-sm rounded px-3 py-2"
                 [class]="testOk() ? 'text-emerald-800 bg-emerald-50 border border-emerald-200'
                                   : 'text-red-700 bg-red-50 border border-red-200'">{{ testResult() }}</p>
            }
          </div>
        </section>

        <!-- ------------------------------ per-state minimums ------------------------------ -->
        <section class="rounded-xl border border-slate-200 bg-white p-5 lg:col-span-2">
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="font-semibold text-slate-900">Minimum order by state</h2>
              <p class="text-sm text-slate-500 mt-0.5">
                Overrides the global minimum above. Leave empty to use one figure everywhere.
              </p>
            </div>
            <button type="button" (click)="addState(m)" class="text-sm text-primary hover:underline shrink-0">
              + Add state
            </button>
          </div>

          @if (!m.stateMinOrders.length) {
            <p class="mt-3 text-sm text-slate-500">No per-state overrides — the global minimum applies everywhere.</p>
          } @else {
            <div class="mt-3 space-y-2">
              @for (row of m.stateMinOrders; track $index) {
                <div class="flex items-center gap-2">
                  <input class="form-input flex-1" [(ngModel)]="row.stateName" placeholder="State name" />
                  <input type="number" min="0" step="1" class="form-input w-32" [(ngModel)]="row.minOrderAmount" />
                  <label class="flex items-center gap-1.5 text-sm text-slate-600 shrink-0">
                    <input type="checkbox" [(ngModel)]="row.isActive" class="w-4 h-4" /> active
                  </label>
                  <button type="button" (click)="removeState(m, $index)"
                          class="text-slate-400 hover:text-red-600 text-lg px-1" aria-label="Remove">×</button>
                </div>
              }
            </div>
          }
        </section>
      </div>

      <div class="mt-5 flex items-center gap-3">
        <button type="button" (click)="save(m)" [disabled]="saving()"
                class="bg-primary hover:bg-primary-dark disabled:bg-slate-300 text-white font-semibold px-6 py-2.5 rounded-lg transition">
          {{ saving() ? 'Saving…' : 'Save settings' }}
        </button>
        @if (savedAt()) { <span class="text-sm text-emerald-600">Saved.</span> }
        @if (error()) { <span class="text-sm text-red-600">{{ error() }}</span> }
      </div>
    }
    </div>
  `,
})
export class AdminShopSettingsComponent {
  private readonly http = inject(HttpClient);
  private readonly url = `${API_BASE_URL}/admin/shop-settings`;

  readonly model = signal<ShopSettings | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly savedAt = signal(false);
  readonly error = signal<string | null>(null);

  readonly testTo = signal('');
  readonly testing = signal(false);
  readonly testResult = signal<string | null>(null);
  readonly testOk = signal(false);

  /** Which image field is mid-upload, so only that button reads "Uploading…". */
  /** Wordmark sizes in rem. 1.25 is text-xl, what the header used before this existed. */
  readonly nameSizes = [
    { value: '1.125', label: 'Small' },
    { value: '1.25',  label: 'Default' },
    { value: '1.5',   label: 'Large' },
    { value: '1.75',  label: 'Extra large' },
    { value: '2',     label: 'Huge' },
  ];

  readonly uploading = signal<'faviconUrl' | 'logoUrl' | 'footerLogoUrl' | null>(null);
  readonly uploadError = signal<string | null>(null);

  constructor() {
    this.http.get<ApiResponse<ShopSettings>>(this.url).subscribe({
      next: (r) => {
        this.model.set(r.data ?? null);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load settings.');
        this.loading.set(false);
      },
    });
  }

  addState(m: ShopSettings): void {
    m.stateMinOrders.push({ id: null, stateName: '', minOrderAmount: 0, isActive: true });
    this.model.set({ ...m });
  }

  removeState(m: ShopSettings, index: number): void {
    m.stateMinOrders.splice(index, 1);
    this.model.set({ ...m });
  }

  save(m: ShopSettings): void {
    this.saving.set(true);
    this.savedAt.set(false);
    this.error.set(null);

    this.http.put<ApiResponse<ShopSettings>>(this.url, m).subscribe({
      next: (r) => {
        this.saving.set(false);
        this.savedAt.set(true);
        // The response never carries the password back, so the field resets to blank —
        // which is correct: blank means "keep what is stored".
        if (r.data) this.model.set(r.data);
      },
      error: (e) => {
        this.saving.set(false);
        this.error.set(e?.error?.message ?? 'Could not save settings.');
      },
    });
  }

  uploadImage(event: Event, m: ShopSettings, field: 'faviconUrl' | 'logoUrl' | 'footerLogoUrl'): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploading.set(field);
    this.uploadError.set(null);

    const form = new FormData();
    form.append('file', file);

    this.http.post<ApiResponse<{ url: string }>>(`${API_BASE_URL}/admin/media`, form).subscribe({
      next: (r) => {
        this.uploading.set(null);
        if (r.data?.url) {
          m[field] = r.data.url;
          this.model.set({ ...m });
        }
        // Clear the input so re-selecting the same file fires change again.
        input.value = '';
      },
      error: (e) => {
        this.uploading.set(null);
        this.uploadError.set(e?.error?.message ?? 'Upload failed.');
        input.value = '';
      },
    });
  }

  sendTest(): void {
    this.testing.set(true);
    this.testResult.set(null);

    this.http
      .post<ApiResponse<unknown>>(`${this.url}/test-email`, { to: this.testTo().trim() })
      .subscribe({
        next: (r) => {
          this.testing.set(false);
          this.testOk.set(true);
          this.testResult.set(r.message ?? 'Test email sent.');
        },
        error: (e) => {
          this.testing.set(false);
          this.testOk.set(false);
          this.testResult.set(e?.error?.message ?? 'Could not send the test email.');
        },
      });
  }
}
