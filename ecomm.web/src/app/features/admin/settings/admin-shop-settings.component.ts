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
        if (r.data) this.model.set(r.data);
      },
      error: (e) => {
        this.saving.set(false);
        this.error.set(e?.error?.message ?? 'Could not save settings.');
      },
    });
  }
}
