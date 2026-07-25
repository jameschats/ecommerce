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
  logoUrl: string;
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
              <input class="form-input" [(ngModel)]="m.siteName" placeholder="e.g. CalendarShop" />
              <span class="text-xs text-slate-500 mt-1 block">
                Shown beside the logo in the header, in the footer and in the copyright line.
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
                Sits before the name. Rendered 36px tall, so a wide transparent PNG works best.
                Set a logo and leave the name blank to show the logo on its own.
              </span>
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
  readonly uploading = signal<'faviconUrl' | 'logoUrl' | null>(null);
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

  uploadImage(event: Event, m: ShopSettings, field: 'faviconUrl' | 'logoUrl'): void {
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
