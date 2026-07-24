import { CurrencyPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { API_BASE_URL } from '../../core/api.config';
import { ApiResponse } from '../../core/models/api-response.model';

interface ManualPaymentDetails {
  orderId: number;
  orderNumber: string;
  amount: number;
  status: string;
  upiId: string | null;
  upiPayeeName: string | null;
  upiIntent: string | null;
  qrCodeDataUri: string | null;
  bankAccountName: string | null;
  bankAccountNumber: string | null;
  bankIfsc: string | null;
  bankName: string | null;
  reportedReference: string | null;
  reportedAt: string | null;
  isConfirmed: boolean;
}

/**
 * Payment instructions after an order is placed (design.md §8).
 *
 * No gateway: the buyer pays by UPI or bank transfer and tells us the reference, and an
 * admin confirms the money arrived. Reporting a payment does not mark the order paid.
 */
@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [FormsModule, CurrencyPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page-container py-8 max-w-2xl">
      @if (loading()) {
        <div class="h-64 rounded-2xl bg-slate-100 animate-pulse"></div>
      } @else if (!details()) {
        <div class="rounded-2xl border border-slate-200 bg-white p-10 text-center">
          <p class="font-medium text-slate-700">We could not find that order.</p>
          <a routerLink="/account/orders" class="mt-2 inline-block text-primary hover:underline">My orders</a>
        </div>
      } @else if (details(); as d) {
        <div class="rounded-2xl border border-slate-200 bg-white overflow-hidden">

          <div class="px-5 py-4 border-b border-slate-200 bg-slate-50">
            <p class="text-sm text-slate-500">Order</p>
            <p class="font-mono font-semibold text-slate-900">{{ d.orderNumber }}</p>
            <p class="mt-2 text-3xl font-bold text-slate-900">
              {{ d.amount | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}
            </p>
          </div>

          @if (d.isConfirmed) {
            <div class="p-8 text-center">
              <div class="w-14 h-14 rounded-full bg-emerald-100 text-emerald-600 grid place-items-center mx-auto text-2xl">✓</div>
              <h2 class="mt-4 text-xl font-bold text-slate-900">Payment confirmed</h2>
              <p class="mt-1 text-slate-600">Thank you. We are preparing your order for dispatch.</p>
            </div>
          } @else {
            <div class="p-5">
              @if (!d.upiId && !d.bankAccountNumber) {
                <p class="rounded-lg bg-amber-50 border border-amber-200 px-4 py-3 text-sm text-amber-900">
                  Payment details are not configured yet. Please contact us to complete your payment —
                  your order is saved.
                </p>
              }

              @if (d.qrCodeDataUri) {
                <div class="text-center">
                  <h2 class="font-semibold text-slate-900">Scan to pay</h2>
                  <p class="text-sm text-slate-500 mt-0.5">GPay, PhonePe, Paytm or any UPI app</p>
                  <img [src]="d.qrCodeDataUri" alt="UPI QR code" width="220" height="220"
                       class="mx-auto mt-3 rounded-lg border border-slate-200" />
                  <p class="mt-2 text-sm text-slate-600">
                    UPI ID: <span class="font-mono font-medium text-slate-900">{{ d.upiId }}</span>
                  </p>
                  @if (d.upiIntent) {
                    <a [href]="d.upiIntent"
                       class="sm:hidden inline-block mt-3 bg-primary text-white font-semibold px-5 py-2.5 rounded-lg">
                      Open UPI app
                    </a>
                  }
                </div>
              }

              @if (d.bankAccountNumber) {
                <div class="mt-6 pt-5 border-t border-slate-100">
                  <h2 class="font-semibold text-slate-900">Or transfer to our bank</h2>
                  <dl class="mt-2 text-sm rounded-lg border border-slate-200 divide-y divide-slate-100">
                    @if (d.bankAccountName) {
                      <div class="flex justify-between px-3 py-2">
                        <dt class="text-slate-500">Account name</dt><dd class="font-medium">{{ d.bankAccountName }}</dd>
                      </div>
                    }
                    <div class="flex justify-between px-3 py-2">
                      <dt class="text-slate-500">Account number</dt>
                      <dd class="font-mono font-medium">{{ d.bankAccountNumber }}</dd>
                    </div>
                    @if (d.bankIfsc) {
                      <div class="flex justify-between px-3 py-2">
                        <dt class="text-slate-500">IFSC</dt><dd class="font-mono font-medium">{{ d.bankIfsc }}</dd>
                      </div>
                    }
                    @if (d.bankName) {
                      <div class="flex justify-between px-3 py-2">
                        <dt class="text-slate-500">Bank</dt><dd class="font-medium">{{ d.bankName }}</dd>
                      </div>
                    }
                  </dl>
                </div>
              }

              <!-- Reporting the reference is what removes most "did they pay?" phone calls -->
              <div class="mt-6 pt-5 border-t border-slate-100">
                @if (d.reportedReference) {
                  <div class="rounded-lg bg-emerald-50 border border-emerald-200 px-4 py-3">
                    <p class="text-sm text-emerald-900">
                      Payment reported — reference
                      <span class="font-mono font-semibold">{{ d.reportedReference }}</span>.
                      We will confirm shortly.
                    </p>
                  </div>
                } @else {
                  <h2 class="font-semibold text-slate-900">Already paid?</h2>
                  <p class="text-sm text-slate-500 mt-0.5">
                    Enter the UPI reference or bank UTR so we can match your payment.
                  </p>
                  <div class="mt-2 flex gap-2">
                    <input type="text" [(ngModel)]="reference" class="form-input flex-1"
                           placeholder="e.g. 412345678901" (keydown.enter)="report(d.orderId)" />
                    <button type="button" (click)="report(d.orderId)"
                            [disabled]="!reference().trim() || reporting()"
                            class="bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300
                                   text-white font-semibold px-5 rounded-lg transition">
                      {{ reporting() ? 'Saving…' : 'I have paid' }}
                    </button>
                  </div>
                  @if (error()) {
                    <p class="mt-2 text-sm text-red-700">{{ error() }}</p>
                  }
                }
              </div>
            </div>
          }
        </div>

        <a routerLink="/account/orders" class="mt-4 inline-block text-sm text-primary hover:underline">
          ← My orders
        </a>
      }
    </section>
  `,
})
export class PaymentComponent {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  readonly details = signal<ManualPaymentDetails | null>(null);
  readonly loading = signal(true);
  readonly reference = signal('');
  readonly reporting = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    const orderId = Number(this.route.snapshot.paramMap.get('orderId'));
    this.load(orderId);
  }

  private load(orderId: number): void {
    this.http.get<ApiResponse<ManualPaymentDetails>>(`${API_BASE_URL}/payments/manual/${orderId}`).subscribe({
      next: (r) => {
        this.details.set(r.data ?? null);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  report(orderId: number): void {
    if (!this.reference().trim() || this.reporting()) return;
    this.reporting.set(true);
    this.error.set(null);

    this.http
      .post<ApiResponse<ManualPaymentDetails>>(`${API_BASE_URL}/payments/manual/${orderId}/report`, {
        referenceNumber: this.reference().trim(),
        proofImageUrl: null,
      })
      .subscribe({
        next: (r) => {
          this.reporting.set(false);
          if (r.data) this.details.set(r.data);
        },
        error: (e) => {
          this.reporting.set(false);
          this.error.set(e?.error?.message ?? 'Could not save that reference. Please try again.');
        },
      });
  }
}
