import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface PendingPayment {
  orderId: number;
  orderNumber: string;
  amount: number;
  status: string;
  referenceNumber: string | null;
  reportedAt: string | null;
  placedAt: string;
  customerNotes: string | null;
}

/**
 * Payments to verify (design.md §8).
 *
 * The manual half of manual payment: the shop checks its bank statement and confirms.
 * Orders where the buyer has reported a reference sort to the top — those are the ones
 * with someone waiting.
 */
@Component({
  selector: 'app-admin-payments',
  standalone: true,
  imports: [CurrencyPipe, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex items-baseline justify-between gap-3">
      <div>
        <h1 class="text-xl font-bold text-slate-900">Payments to verify</h1>
        <p class="text-sm text-slate-500 mt-0.5">
          Confirming marks the order paid and commits the reserved stock.
        </p>
      </div>
      <button type="button" (click)="load()" class="text-sm text-primary hover:underline">Refresh</button>
    </div>

    @if (loading()) {
      <div class="mt-4 space-y-2">
        @for (i of [1,2,3]; track i) { <div class="h-16 rounded-lg bg-slate-100 animate-pulse"></div> }
      </div>
    } @else if (!rows().length) {
      <div class="mt-4 rounded-xl border border-slate-200 bg-white py-16 text-center">
        <p class="font-medium text-slate-700">Nothing awaiting payment.</p>
      </div>
    } @else {
      <p class="mt-3 text-sm text-slate-500">
        {{ reportedCount() }} reported · {{ rows().length - reportedCount() }} not yet reported
      </p>

      <div class="mt-2 overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table class="w-full text-sm">
          <thead class="bg-slate-50 text-slate-600 text-left">
            <tr>
              <th class="px-3 py-2.5 font-semibold">Order</th>
              <th class="px-3 py-2.5 font-semibold">Placed</th>
              <th class="px-3 py-2.5 font-semibold text-right">Amount</th>
              <th class="px-3 py-2.5 font-semibold">Buyer's reference</th>
              <th class="px-3 py-2.5 font-semibold">Customer</th>
              <th class="px-3 py-2.5 font-semibold text-right">Action</th>
            </tr>
          </thead>
          <tbody>
            @for (r of rows(); track r.orderId) {
              <tr class="border-t border-slate-100" [class]="r.referenceNumber ? 'bg-amber-50/50' : ''">
                <td class="px-3 py-2.5 font-mono font-medium text-slate-900">{{ r.orderNumber }}</td>
                <td class="px-3 py-2.5 text-slate-600">{{ r.placedAt | date: 'dd MMM, HH:mm' }}</td>
                <td class="px-3 py-2.5 text-right font-semibold">
                  {{ r.amount | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}
                </td>
                <td class="px-3 py-2.5">
                  @if (r.referenceNumber) {
                    <span class="font-mono font-medium text-slate-900">{{ r.referenceNumber }}</span>
                    <span class="block text-xs text-slate-500">{{ r.reportedAt | date: 'dd MMM, HH:mm' }}</span>
                  } @else {
                    <span class="text-slate-400">not reported</span>
                  }
                </td>
                <td class="px-3 py-2.5 text-xs text-slate-600 whitespace-pre-line max-w-[260px]">{{ r.customerNotes }}</td>
                <td class="px-3 py-2.5 text-right">
                  <button type="button" (click)="confirm(r)" [disabled]="busyId() === r.orderId"
                          class="bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300 text-white
                                 text-xs font-semibold px-3 py-1.5 rounded transition">
                    {{ busyId() === r.orderId ? 'Confirming…' : 'Confirm payment' }}
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    @if (error()) {
      <p class="mt-3 text-sm text-red-700 bg-red-50 border border-red-200 rounded px-3 py-2">{{ error() }}</p>
    }
  `,
})
export class AdminPaymentsComponent {
  private readonly http = inject(HttpClient);

  readonly rows = signal<PendingPayment[]>([]);
  readonly loading = signal(true);
  readonly busyId = signal<number | null>(null);
  readonly error = signal<string | null>(null);

  readonly reportedCount = computed(() => this.rows().filter((r) => r.referenceNumber).length);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<PendingPayment[]>>(`${API_BASE_URL}/admin/payments/pending`).subscribe({
      next: (r) => {
        this.rows.set(r.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load pending payments.');
        this.loading.set(false);
      },
    });
  }

  confirm(row: PendingPayment): void {
    // Money is involved and this releases stock — worth one deliberate click.
    if (!confirm(`Confirm payment of ₹${row.amount.toFixed(2)} for ${row.orderNumber}?`)) return;

    this.busyId.set(row.orderId);
    this.error.set(null);

    this.http.post<ApiResponse<unknown>>(`${API_BASE_URL}/admin/payments/${row.orderId}/confirm`, {}).subscribe({
      next: () => {
        this.busyId.set(null);
        this.rows.update((rows) => rows.filter((r) => r.orderId !== row.orderId));
      },
      error: (e) => {
        this.busyId.set(null);
        this.error.set(e?.error?.message ?? 'Could not confirm that payment.');
      },
    });
  }
}
