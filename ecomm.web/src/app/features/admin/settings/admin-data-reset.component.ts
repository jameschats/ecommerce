import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface ResetPreview {
  isLive: boolean; liveSince: string | null;
  orders: number; orderValue: number; orderItems: number; statusHistory: number;
  invoices: number; payments: number; shipments: number; reviews: number; creditNotes: number;
  inventoryTransactions: number; reservedUnits: number; productsHoldingStock: number;
  notifications: number; carts: number; sessions: number; otps: number; wishlist: number;
  customerAccounts: number; addresses: number;
  pageViews: number; searchLogs: number; importJobs: number; contacts: number;
  invoiceCounter: number; orderCounter: number;
}

interface ResetResult { removed: ResetPreview; notes: string[]; }

const CONFIRM = 'DELETE ALL TRANSACTIONS';

@Component({
  selector: 'app-admin-data-reset',
  imports: [FormsModule, DecimalPipe, DatePipe],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Go live</h1>
      <p class="text-sm text-slate-500 mt-1">
        Clear the test trading history so the shop opens with a clean ledger. The catalogue,
        settings, pages and contacts are never touched.
      </p>

      @if (loading()) {
        <div class="mt-8 text-center text-slate-400">Loading…</div>
      } @else if (preview(); as p) {

        @if (result(); as r) {
          <div class="mt-6 rounded-xl border border-green-200 bg-green-50 p-5">
            <h2 class="font-semibold text-green-900">Done.</h2>
            <p class="text-sm text-green-800 mt-1">
              Removed {{ r.removed.orders }} order(s) worth ₹{{ r.removed.orderValue | number: '1.2-2' }},
              {{ r.removed.invoices }} invoice(s) and {{ r.removed.payments }} payment(s).
            </p>
            <ul class="mt-3 space-y-1.5 text-sm text-green-900 list-disc pl-5">
              @for (n of r.notes; track n) { <li>{{ n }}</li> }
            </ul>
          </div>
        }

        @if (p.isLive) {
          <!--
            The one-way door, closed. There is no button here to reopen it: a switch that can be
            flicked back is not a safeguard. Undoing this needs someone at the database.
          -->
          <div class="mt-6 rounded-xl border border-slate-200 bg-white p-5">
            <h2 class="font-semibold text-slate-900">This shop is live</h2>
            <p class="text-sm text-slate-600 mt-1">
              Live since {{ p.liveSince | date: 'd MMMM y' }}. Trading records can no longer be
              deleted in bulk from admin. Individual orders can still be removed one at a time
              from the Orders screen, and anything larger is a restore from backup.
            </p>
            <dl class="grid grid-cols-3 gap-4 mt-5 text-sm">
              <div><dt class="text-slate-500">Orders</dt><dd class="font-semibold text-slate-900">{{ p.orders }}</dd></div>
              <div><dt class="text-slate-500">Invoices</dt><dd class="font-semibold text-slate-900">{{ p.invoices }}</dd></div>
              <div><dt class="text-slate-500">Payments</dt><dd class="font-semibold text-slate-900">{{ p.payments }}</dd></div>
            </dl>
          </div>
        } @else {

          <!-- What always goes -->
          <div class="mt-6 rounded-xl border border-slate-200 bg-white overflow-hidden">
            <div class="px-5 py-3 border-b border-slate-100">
              <h2 class="font-semibold text-slate-900">Always removed</h2>
              <p class="text-xs text-slate-500 mt-0.5">Everything the shop recorded while you were testing.</p>
            </div>
            <dl class="divide-y divide-slate-100 text-sm">
              <div class="flex justify-between px-5 py-2.5">
                <dt class="text-slate-700">Orders <span class="text-slate-400">— worth ₹{{ p.orderValue | number: '1.2-2' }}</span></dt>
                <dd class="font-semibold text-slate-900">{{ p.orders }}</dd>
              </div>
              <div class="flex justify-between px-5 py-2.5"><dt class="text-slate-700">Order lines &amp; status history</dt><dd class="font-semibold text-slate-900">{{ p.orderItems + p.statusHistory }}</dd></div>
              <div class="flex justify-between px-5 py-2.5 text-red-800"><dt>Invoices</dt><dd class="font-semibold">{{ p.invoices }}</dd></div>
              <div class="flex justify-between px-5 py-2.5 text-red-800"><dt>Payments</dt><dd class="font-semibold">{{ p.payments }}</dd></div>
              <div class="flex justify-between px-5 py-2.5"><dt class="text-slate-700">Shipments</dt><dd class="font-semibold text-slate-900">{{ p.shipments }}</dd></div>
              <div class="flex justify-between px-5 py-2.5"><dt class="text-slate-700">Reviews &amp; credit notes</dt><dd class="font-semibold text-slate-900">{{ p.reviews + p.creditNotes }}</dd></div>
              <div class="flex justify-between px-5 py-2.5"><dt class="text-slate-700">Stock movement history</dt><dd class="font-semibold text-slate-900">{{ p.inventoryTransactions }}</dd></div>
              <div class="flex justify-between px-5 py-2.5"><dt class="text-slate-700">Carts &amp; wishlists</dt><dd class="font-semibold text-slate-900">{{ p.carts + p.wishlist }}</dd></div>
              <div class="flex justify-between px-5 py-2.5"><dt class="text-slate-700">Notifications</dt><dd class="font-semibold text-slate-900">{{ p.notifications }}</dd></div>
              <div class="flex justify-between px-5 py-2.5"><dt class="text-slate-700">Sign-in sessions &amp; OTPs</dt><dd class="font-semibold text-slate-900">{{ p.sessions + p.otps }}</dd></div>
            </dl>
          </div>

          <!-- The three things a plain delete gets wrong, stated as facts about this database -->
          <div class="mt-4 rounded-xl border border-blue-200 bg-blue-50 p-5 text-sm text-blue-900">
            <h2 class="font-semibold">What this also puts right</h2>
            <ul class="mt-2 space-y-1.5 list-disc pl-5">
              @if (p.reservedUnits) {
                <li>
                  <strong>{{ p.reservedUnits }} unit(s)</strong> across {{ p.productsHoldingStock }} product(s)
                  are currently reserved by those test orders. Deleting orders does not release them —
                  this does, putting them back into available stock.
                </li>
              }
              <li>
                Invoice numbering restarts. Without this your first real invoice would be
                <strong>INV-{{ year }}-{{ pad(p.invoiceCounter) }}</strong>.
              </li>
              <li>Reviews and credit notes are removed explicitly — they survive their orders otherwise.</li>
            </ul>
          </div>

          <!-- Optional groups -->
          <div class="mt-4 rounded-xl border border-slate-200 bg-white p-5">
            <h2 class="font-semibold text-slate-900">Also remove</h2>
            <div class="mt-3 space-y-3 text-sm">
              <label class="flex items-start gap-3">
                <input type="checkbox" [(ngModel)]="optCustomers" class="mt-1" />
                <span>
                  <span class="text-slate-800 font-medium">Customer accounts ({{ p.customerAccounts }})</span>
                  <span class="block text-slate-500 text-[13px]">
                    And their {{ p.addresses }} saved address(es). Staff and admin accounts are always kept,
                    including the one you are signed in with.
                  </span>
                </span>
              </label>
              <label class="flex items-start gap-3">
                <input type="checkbox" [(ngModel)]="optTraffic" class="mt-1" />
                <span>
                  <span class="text-slate-800 font-medium">Traffic &amp; search history ({{ p.pageViews + p.searchLogs }})</span>
                  <span class="block text-slate-500 text-[13px]">Page views and searches from your own testing, which would otherwise skew your first month.</span>
                </span>
              </label>
              <label class="flex items-start gap-3">
                <input type="checkbox" [(ngModel)]="optImports" class="mt-1" />
                <span>
                  <span class="text-slate-800 font-medium">Import history ({{ p.importJobs }} job(s))</span>
                  <span class="block text-slate-500 text-[13px]">Records of how the catalogue was loaded. The products themselves stay.</span>
                </span>
              </label>
              <label class="flex items-start gap-3">
                <input type="checkbox" [(ngModel)]="optContacts" class="mt-1" />
                <span>
                  <span class="text-slate-800 font-medium">Contact &amp; enquiry messages ({{ p.contacts }})</span>
                  <span class="block text-slate-500 text-[13px]">Only tick this if the messages you have are test ones.</span>
                </span>
              </label>
            </div>
          </div>

          <!-- Confirm -->
          <div class="mt-4 rounded-xl border border-red-200 bg-white p-5">
            <p class="text-sm text-slate-700">
              This cannot be undone from here. Take a database backup first if there is anything
              in the list you are unsure about.
            </p>
            <label class="block mt-3 text-sm">
              <span class="text-slate-600">Type <strong class="text-slate-900">{{ confirmPhrase }}</strong> to confirm</span>
              <input [(ngModel)]="typed" class="input w-full mt-1" [placeholder]="confirmPhrase" />
            </label>
            @if (error()) { <p class="text-sm text-red-600 mt-2">{{ error() }}</p> }
            <button type="button" (click)="run()" [disabled]="busy() || typed.trim() !== confirmPhrase"
                    class="mt-3 px-4 py-2 rounded-lg bg-red-600 text-white text-sm font-medium disabled:opacity-40">
              {{ busy() ? 'Clearing…' : 'Clear all trading records' }}
            </button>
          </div>

          <!-- The door -->
          <div class="mt-4 rounded-xl border border-slate-200 bg-slate-50 p-5">
            <h2 class="font-semibold text-slate-900">Open for business</h2>
            <p class="text-sm text-slate-600 mt-1">
              When the shop is taking real orders, mark it live. That switches this whole screen
              off permanently — real invoices and payments are records you are expected to keep,
              and no admin screen should be able to wipe them. There is no way to switch it back
              on from here.
            </p>
            <button type="button" (click)="goLive()" [disabled]="busy()"
                    class="mt-3 px-4 py-2 rounded-lg bg-slate-900 text-white text-sm font-medium disabled:opacity-40">
              Mark this shop live
            </button>
          </div>
        }
      }
    </div>
  `,
})
export class AdminDataResetComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly url = `${API_BASE_URL}/admin/data-reset`;

  readonly preview = signal<ResetPreview | null>(null);
  readonly result = signal<ResetResult | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly confirmPhrase = CONFIRM;
  readonly year = new Date().getFullYear();

  /** Named apart from the global confirm() used in goLive(), which reads badly otherwise. */
  typed = '';
  optCustomers = false;
  optTraffic = false;
  optImports = false;
  optContacts = false;

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<ResetPreview>>(this.url).subscribe({
      next: (r) => { this.preview.set(r.data ?? null); this.loading.set(false); },
      error: (e) => { this.error.set(e?.error?.message ?? 'Could not load.'); this.loading.set(false); },
    });
  }

  /** Zero-padded to five, matching how the server builds INV-{year}-{id:D5}. */
  pad(n: number): string {
    return String(n).padStart(5, '0');
  }

  run(): void {
    this.busy.set(true);
    this.error.set(null);
    this.http.post<ApiResponse<ResetResult>>(this.url, {
      confirm: this.typed,
      customerAccounts: this.optCustomers,
      traffic: this.optTraffic,
      importHistory: this.optImports,
      contacts: this.optContacts,
    }).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.result.set(r.data ?? null);
        this.typed = '';
        this.load();
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(e?.error?.message ?? 'Could not clear the data.');
      },
    });
  }

  goLive(): void {
    if (!confirm('Mark this shop live? This permanently switches off bulk deletion of orders, invoices and payments. It cannot be undone from admin.')) return;

    this.busy.set(true);
    this.error.set(null);
    this.http.post<ApiResponse<unknown>>(`${this.url}/go-live`, {}).subscribe({
      next: () => { this.busy.set(false); this.result.set(null); this.load(); },
      error: (e) => { this.busy.set(false); this.error.set(e?.error?.message ?? 'Could not mark live.'); },
    });
  }
}
