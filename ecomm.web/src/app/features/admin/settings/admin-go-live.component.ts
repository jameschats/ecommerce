import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { GoLiveResetRequest, GoLiveService, GoLiveSummary } from '../../../core/services/go-live.service';

const CONFIRMATION_PHRASE = 'DELETE ALL TRANSACTIONS';

/**
 * Settings → Go live. Clears everything the store recorded while testing — orders, payments,
 * invoices, notifications, sessions — so the ledger opens clean. Catalogue/settings/pages/
 * contacts are never touched by the always-removed set. "Mark this shop live" is a one-way
 * switch: once GoneLiveAt is set, this screen locks permanently (backend refuses both actions
 * too, so there's no path back even by forging a request).
 */
@Component({
  selector: 'app-admin-go-live',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <div class="flex items-center justify-between gap-3 mb-1">
        <h1 class="text-xl font-bold text-slate-900">Go live</h1>
        <a routerLink="/admin/settings" class="text-sm text-slate-500 hover:underline">← Settings</a>
      </div>
      <p class="text-sm text-slate-500 mb-5">Clear the test trading history so the shop opens with a clean ledger. The catalogue, settings, pages and contacts are never touched.</p>

      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) {
        <div class="p-10 text-center text-slate-400">Loading…</div>
      } @else if (summary(); as s) {
        @if (s.isLive) {
          <div class="bg-white border border-slate-200 rounded-xl p-6 text-center">
            <div class="text-3xl mb-2">🚀</div>
            <h2 class="font-semibold text-slate-800 mb-1">This shop is live</h2>
            <p class="text-sm text-slate-500">Marked live on {{ s.goneLiveAt | date:'mediumDate' }}. The data-reset screen is permanently closed — real invoices and payments are records you're expected to keep.</p>
          </div>
        } @else {
          <!-- Always removed -->
          <div class="bg-white border border-slate-200 rounded-xl p-5 mb-4">
            <h2 class="font-medium text-slate-800">Always removed</h2>
            <p class="text-xs text-slate-400 mb-3">Everything the shop recorded while you were testing.</p>
            <div class="divide-y divide-slate-100 text-sm">
              <div class="flex justify-between py-1.5"><span class="text-slate-600">Orders @if (s.ordersWorth) { <span class="text-slate-400">— worth {{ s.ordersWorth | currency:'INR':'symbol':'1.2-2' }}</span> }</span><span class="font-medium">{{ s.orders }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-slate-600">Order lines &amp; status history</span><span class="font-medium">{{ s.orderLinesAndStatusHistory }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-red-600">Invoices</span><span class="font-medium">{{ s.invoices }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-red-600">Payments</span><span class="font-medium">{{ s.payments }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-slate-600">Shipments</span><span class="font-medium">{{ s.shipments }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-slate-600">Reviews &amp; credit notes</span><span class="font-medium">{{ s.reviewsAndCreditNotes }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-slate-600">Stock movement history</span><span class="font-medium">{{ s.stockMovementHistory }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-slate-600">Carts &amp; wishlists</span><span class="font-medium">{{ s.cartsAndWishlists }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-slate-600">Notifications</span><span class="font-medium">{{ s.notifications }}</span></div>
              <div class="flex justify-between py-1.5"><span class="text-slate-600">Sign-in sessions &amp; OTPs</span><span class="font-medium">{{ s.signInSessionsAndOtps }}</span></div>
            </div>
          </div>

          <!-- What this also puts right -->
          <div class="bg-blue-50 border border-blue-200 rounded-xl p-5 mb-4">
            <h2 class="font-medium text-blue-900 mb-2">What this also puts right</h2>
            <ul class="text-sm text-blue-900 space-y-1.5 list-disc pl-4">
              @if (s.reservedUnits) {
                <li><b>{{ s.reservedUnits }} unit(s)</b> across {{ s.productsWithReservedUnits }} product(s) are currently reserved by those test orders. Deleting orders does not release them — this does, putting them back into available stock.</li>
              }
              <li>Invoice numbering restarts. Without this your first real invoice would be <b>{{ s.nextInvoicePreview }}</b>.</li>
              <li>Reviews and credit notes are removed explicitly — they survive their orders otherwise.</li>
            </ul>
          </div>

          <!-- Also remove -->
          <div class="bg-white border border-slate-200 rounded-xl p-5 mb-4">
            <h2 class="font-medium text-slate-800 mb-3">Also remove</h2>
            <div class="space-y-3">
              <label class="flex items-start gap-2.5">
                <input type="checkbox" [(ngModel)]="opts.includeCustomers" class="mt-0.5" />
                <span class="text-sm">
                  <span class="font-medium text-slate-800">Customer accounts ({{ s.customerAccounts }})</span>
                  <p class="text-xs text-slate-400">And their {{ s.customerAddresses }} saved address(es). Staff and admin accounts are always kept, including the one you are signed in with.</p>
                </span>
              </label>
              <label class="flex items-start gap-2.5">
                <input type="checkbox" [(ngModel)]="opts.includeTraffic" class="mt-0.5" />
                <span class="text-sm">
                  <span class="font-medium text-slate-800">Traffic &amp; search history ({{ s.trafficAndSearchHistory }})</span>
                  <p class="text-xs text-slate-400">Page views and searches from your own testing, which would otherwise skew your first month.</p>
                </span>
              </label>
              <label class="flex items-start gap-2.5">
                <input type="checkbox" [(ngModel)]="opts.includeImportHistory" class="mt-0.5" />
                <span class="text-sm">
                  <span class="font-medium text-slate-800">Import history ({{ s.importJobs }} job(s))</span>
                  <p class="text-xs text-slate-400">Records of how the catalogue was loaded. The products themselves stay.</p>
                </span>
              </label>
              <label class="flex items-start gap-2.5">
                <input type="checkbox" [(ngModel)]="opts.includeContactMessages" class="mt-0.5" />
                <span class="text-sm">
                  <span class="font-medium text-slate-800">Contact &amp; enquiry messages ({{ s.contactMessages }})</span>
                  <p class="text-xs text-slate-400">Only tick this if the messages you have are test ones.</p>
                </span>
              </label>
            </div>
          </div>

          <!-- Confirm & clear -->
          <div class="bg-white border border-red-200 rounded-xl p-5 mb-6">
            <p class="text-sm text-slate-700 mb-3">This cannot be undone from here. Take a database backup first if there is anything in the list you are unsure about.</p>
            <label class="text-sm text-slate-600 mb-1 block">Type <b>{{ phrase }}</b> to confirm</label>
            <input [(ngModel)]="confirmText" [placeholder]="phrase" class="input w-full mb-3" />
            <button type="button" (click)="reset()" [disabled]="!canReset() || resetting()"
                    class="px-4 py-2.5 rounded-lg font-medium text-white"
                    [class]="canReset() && !resetting() ? 'bg-red-600 hover:bg-red-700' : 'bg-red-300 cursor-not-allowed'">
              {{ resetting() ? 'Clearing…' : 'Clear all trading records' }}
            </button>
          </div>

          <!-- Open for business -->
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <h2 class="font-medium text-slate-800 mb-1">Open for business</h2>
            <p class="text-sm text-slate-500 mb-3">When the shop is taking real orders, mark it live. That switches this whole screen off permanently — real invoices and payments are records you are expected to keep, and no admin screen should be able to wipe them. There is no way to switch it back on from here.</p>
            <button type="button" (click)="markLive()" [disabled]="marking()" class="px-4 py-2.5 rounded-lg font-medium text-white bg-slate-900 hover:bg-slate-800 disabled:opacity-50">
              {{ marking() ? 'Marking live…' : 'Mark this shop live' }}
            </button>
          </div>
        }
      }
    </div>
  `,
})
export class AdminGoLiveComponent implements OnInit {
  private readonly api = inject(GoLiveService);

  readonly phrase = CONFIRMATION_PHRASE;
  readonly loading = signal(true);
  readonly resetting = signal(false);
  readonly marking = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly summary = signal<GoLiveSummary | null>(null);

  confirmText = '';
  opts = { includeCustomers: false, includeTraffic: false, includeImportHistory: false, includeContactMessages: false };

  readonly canReset = computed(() => this.confirmText.trim() === this.phrase);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.summary().subscribe({
      next: (s) => { this.summary.set(s); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.message ?? 'Could not load.'); this.loading.set(false); },
    });
  }

  reset(): void {
    if (!this.canReset()) return;
    if (typeof window !== 'undefined' && !window.confirm('This permanently deletes all trading records. Continue?')) return;
    this.resetting.set(true); this.error.set(null); this.message.set(null);
    const body: GoLiveResetRequest = { ...this.opts, confirmationText: this.confirmText.trim() };
    this.api.reset(body).subscribe({
      next: (r) => {
        this.resetting.set(false);
        this.confirmText = '';
        this.opts = { includeCustomers: false, includeTraffic: false, includeImportHistory: false, includeContactMessages: false };
        this.message.set(`Cleared ${r.ordersRemoved} order(s) and everything tied to them.`);
        this.load();
      },
      error: (e) => { this.resetting.set(false); this.error.set(e?.error?.message ?? 'Reset failed.'); },
    });
  }

  markLive(): void {
    if (typeof window !== 'undefined' && !window.confirm('Mark this shop live? This closes the Go live screen permanently — there is no way to switch it back.')) return;
    this.marking.set(true); this.error.set(null); this.message.set(null);
    this.api.markLive().subscribe({
      next: () => { this.marking.set(false); this.message.set('This shop is now live.'); this.load(); },
      error: (e) => { this.marking.set(false); this.error.set(e?.error?.message ?? 'Failed.'); },
    });
  }
}
