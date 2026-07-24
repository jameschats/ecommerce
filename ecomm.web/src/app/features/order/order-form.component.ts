import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  QuickOrderCheckoutService,
  QuickOrderQuote,
} from '../../core/services/quick-order-checkout.service';
import { QuickOrderService } from '../../core/services/quick-order.service';

/**
 * The order form at the foot of the price list (design.md §7).
 *
 * Every number on the right comes from the server's quote, not the browser's running
 * totals — the table's arithmetic exists to keep typing instant, but what the buyer
 * commits to has to be the server's figure.
 */
@Component({
  selector: 'app-order-form',
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="order-form" class="page-container py-8">
      <div class="rounded-2xl border border-slate-200 bg-white overflow-hidden">
        <div class="bg-slate-50 border-b border-slate-200 px-5 py-3">
          <h2 class="font-semibold text-slate-900">Please check your order</h2>
          <p class="text-sm text-slate-500 mt-0.5">
            Fill in your delivery details. We will contact you to confirm before dispatch.
          </p>
        </div>

        @if (!lineCount()) {
          <div class="px-5 py-12 text-center">
            <p class="font-medium text-slate-700">Your estimate is empty.</p>
            <p class="text-sm text-slate-500 mt-1">Enter a quantity against any item above to begin.</p>
          </div>
        } @else {
          <div class="grid lg:grid-cols-[1fr_360px] gap-6 p-5">

            <!-- ------------------------------- details ------------------------------- -->
            <div class="grid sm:grid-cols-2 gap-4">
              <label class="block">
                <span class="form-label">State <span class="text-red-500">*</span></span>
                <select [(ngModel)]="state" name="state" class="form-input">
                  <option value="">Select state…</option>
                  @for (s of states(); track s) { <option [value]="s">{{ s }}</option> }
                </select>
              </label>

              <label class="block">
                <span class="form-label">City</span>
                <input type="text" [(ngModel)]="city" name="city" class="form-input" placeholder="City" />
              </label>

              <label class="block">
                <span class="form-label">Name <span class="text-red-500">*</span></span>
                <input type="text" [(ngModel)]="name" name="name" class="form-input" placeholder="Full name" />
              </label>

              <label class="block">
                <span class="form-label">Mobile No <span class="text-red-500">*</span></span>
                <input type="tel" [(ngModel)]="mobile" name="mobile" maxlength="10" inputmode="numeric"
                       class="form-input" placeholder="10 digits, no +91" />
                @if (mobile() && !mobileValid()) {
                  <span class="text-xs text-red-600 mt-1 block">Enter exactly 10 digits, without +91 or spaces.</span>
                }
              </label>

              <label class="block">
                <span class="form-label">Email</span>
                <input type="email" [(ngModel)]="email" name="email" class="form-input" placeholder="Optional" />
              </label>

              <label class="block sm:col-span-2">
                <span class="form-label">Address <span class="text-red-500">*</span></span>
                <textarea [(ngModel)]="address" name="address" rows="3" class="form-input"
                          placeholder="Delivery address"></textarea>
              </label>
            </div>

            <!-- ------------------------------- summary ------------------------------- -->
            <div class="lg:border-l lg:border-slate-200 lg:pl-6">
              <dl class="space-y-2 text-sm">
                <div class="flex justify-between">
                  <dt class="text-slate-500">Net Total</dt>
                  <dd class="text-slate-700">{{ quote().netTotal | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}</dd>
                </div>
                <div class="flex justify-between">
                  <dt class="text-slate-500">Discount Total</dt>
                  <dd class="text-emerald-600">− {{ quote().discountTotal | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}</dd>
                </div>
                <div class="flex justify-between font-medium">
                  <dt class="text-slate-700">Sub Total</dt>
                  <dd class="text-slate-900">{{ quote().subTotal | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}</dd>
                </div>

                @if (quote().minOrderAmount > 0) {
                  <div class="flex justify-between">
                    <dt [class]="quote().meetsMinimum ? 'text-slate-500' : 'text-red-600 font-medium'">
                      Min. Order Amount
                    </dt>
                    <dd [class]="quote().meetsMinimum ? 'text-slate-700' : 'text-red-600 font-semibold'">
                      {{ quote().minOrderAmount | currency: 'INR' : 'symbol-narrow' : '1.0-0' }}
                    </dd>
                  </div>
                }

                <div class="flex justify-between">
                  <dt class="text-slate-500">Packing Charges ({{ quote().packingChargePct }}%)</dt>
                  <dd class="text-slate-700">{{ quote().packingCharges | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}</dd>
                </div>
                <div class="flex justify-between">
                  <dt class="text-slate-500">Round Off</dt>
                  <dd class="text-slate-700">{{ quote().roundOff | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}</dd>
                </div>
                <div class="flex justify-between pt-2 border-t border-slate-200">
                  <dt class="font-semibold text-slate-800">Overall Amount</dt>
                  <dd class="font-bold text-xl text-slate-900">
                    {{ quote().overallAmount | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}
                  </dd>
                </div>
              </dl>

              @if (quoting()) {
                <p class="text-xs text-slate-400 mt-2">Updating totals…</p>
              }

              @for (w of quote().warnings; track w) {
                <p class="mt-2 text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded px-3 py-2">{{ w }}</p>
              }

              @if (formErrors().length) {
                <ul class="mt-3 text-sm text-red-700 bg-red-50 border border-red-200 rounded px-3 py-2 space-y-0.5">
                  @for (e of formErrors(); track e) { <li>• {{ e }}</li> }
                </ul>
              }

              <button type="button" (click)="submit()" [disabled]="!canSubmit()"
                      class="mt-4 w-full bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300
                             disabled:cursor-not-allowed text-white font-semibold py-3 rounded-lg transition">
                Submit order
              </button>

              <p class="mt-2 text-xs text-slate-500 text-center">
                You will be asked to verify your mobile or email before the order is placed.
              </p>
            </div>
          </div>
        }
      </div>
    </section>
  `,
})
export class OrderFormComponent {
  private readonly quickOrder = inject(QuickOrderService);
  private readonly checkout = inject(QuickOrderCheckoutService);

  readonly lineCount = this.quickOrder.lineCount;

  readonly state = signal('');
  readonly city = signal('');
  readonly name = signal('');
  readonly mobile = signal('');
  readonly email = signal('');
  readonly address = signal('');

  readonly quote = signal<QuickOrderQuote>({
    lines: [], itemCount: 0, totalUnits: 0, netTotal: 0, discountTotal: 0, subTotal: 0,
    minOrderAmount: 0, packingChargePct: 0, packingCharges: 0, roundOff: 0, overallAmount: 0,
    meetsMinimum: false, warnings: [],
  });
  readonly quoting = signal(false);

  private readonly config = toSignal(this.checkout.getConfig(), { initialValue: null });
  readonly states = computed(() => this.config()?.states ?? []);

  /** The reference site's rule, and a good one — it keeps a lot of bad data out. */
  readonly mobileValid = computed(() => /^\d{10}$/.test(this.mobile()));

  readonly formErrors = computed(() => {
    const errors: string[] = [];
    if (!this.state()) errors.push('Select your state.');
    if (!this.name().trim()) errors.push('Enter your name.');
    if (!this.mobileValid()) errors.push('Enter a valid 10-digit mobile number.');
    if (!this.address().trim()) errors.push('Enter your delivery address.');
    if (this.quote().minOrderAmount > 0 && !this.quote().meetsMinimum) {
      errors.push('Your order is below the minimum for the selected state.');
    }
    return errors;
  });

  readonly canSubmit = computed(() => this.lineCount() > 0 && this.formErrors().length === 0 && !this.quoting());

  constructor() {
    // Re-price whenever the basket or the state changes. The state matters because the
    // minimum-order rule can differ by state.
    effect(() => {
      const lines = this.quickOrder.lines().map((l) => ({
        productId: l.item.productId,
        quantity: l.qty,
      }));
      const state = this.state() || null;

      if (!lines.length) {
        this.quote.update((q) => ({ ...q, lines: [], itemCount: 0, subTotal: 0, overallAmount: 0 }));
        return;
      }

      this.quoting.set(true);
      this.checkout.quote(lines, state).subscribe((q) => {
        this.quote.set(q);
        this.quoting.set(false);
      });
    });
  }

  submit(): void {
    // The OTP login gate and order creation land in the next slice (design.md §7).
    // Until then this is deliberately inert rather than pretending to place an order.
    alert(
      'Order placement is not wired up yet.\n\n' +
        `Overall amount: ₹${this.quote().overallAmount.toFixed(2)}\n` +
        'The next step adds mobile/email OTP verification and creates the order.',
    );
  }
}
