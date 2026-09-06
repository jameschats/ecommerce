import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  PlacedOrder,
  QuickOrderCheckoutService,
  QuickOrderQuote,
} from '../../core/services/quick-order-checkout.service';
import { QuickOrderService } from '../../core/services/quick-order.service';
import { AuthService } from '../../core/services/auth.service';
import { OtpGateComponent } from './otp-gate.component';

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
  imports: [FormsModule, CurrencyPipe, RouterLink, OtpGateComponent],
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

        @if (placed(); as order) {
          <div class="px-5 py-12 text-center">
            <div class="w-14 h-14 rounded-full bg-emerald-100 text-emerald-600 grid place-items-center mx-auto text-2xl">✓</div>
            <h3 class="mt-4 text-xl font-bold text-slate-900">Order placed</h3>
            <p class="mt-1 text-slate-600">
              Your order number is <span class="font-mono font-semibold text-slate-900">{{ order.orderNumber }}</span>
            </p>
            <p class="mt-1 text-2xl font-bold text-slate-900">
              {{ order.overallAmount | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}
            </p>
            <p class="mt-4 text-sm text-slate-500 max-w-md mx-auto">
              Pay by UPI or bank transfer to complete your order. We will contact you on the
              number you provided to confirm.
            </p>
            <!-- Auto-continues to the payment screen a moment after this acknowledgement is
                 shown — "Pay now" stays as an immediate way in, not the only way in, in case
                 the redirect is ever slow to fire. -->
            <p class="mt-4 text-xs text-slate-400">Taking you to payment…</p>
            <a [routerLink]="['/order', order.orderId, 'pay']"
               class="inline-block mt-2 bg-primary hover:bg-primary-dark text-white font-semibold px-6 py-2.5 rounded-lg transition">
              Pay now
            </a>
            <a routerLink="/account/orders" class="block mt-3 text-sm text-primary hover:underline">
              View my orders
            </a>
          </div>
        } @else if (!lineCount()) {
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

              <label class="block">
                <span class="form-label">Business name</span>
                <input type="text" [(ngModel)]="businessName" name="businessName" class="form-input"
                       placeholder="Shop or firm name (optional)" />
              </label>

              <label class="block">
                <span class="form-label">GSTIN</span>
                <input type="text" [(ngModel)]="gstin" name="gstin" maxlength="15"
                       class="form-input uppercase" placeholder="Optional — printed on your bill" />
                @if (gstin() && !gstinValid()) {
                  <span class="text-xs text-red-600 mt-1 block">A GSTIN is 15 characters.</span>
                }
              </label>

              <label class="block sm:col-span-2">
                <span class="form-label">Address <span class="text-red-500">*</span></span>
                <textarea [(ngModel)]="address" name="address" rows="3" class="form-input"
                          placeholder="Billing address"></textarea>
              </label>

              <!-- Ship-to is opt-in. Most orders go to the address just typed, and a second
                   set of fields shown by default would be five more boxes to scroll past on
                   the screen a dealer uses every week. -->
              <label class="block sm:col-span-2 flex items-center gap-2 cursor-pointer">
                <input type="checkbox" [ngModel]="shipToDifferent()" (ngModelChange)="shipToDifferent.set($event)"
                       name="shipDiff" class="w-4 h-4" />
                <span class="text-sm text-slate-700">Deliver to a different address</span>
              </label>

              @if (shipToDifferent()) {
                <div class="sm:col-span-2 grid sm:grid-cols-2 gap-4 rounded-xl border border-slate-200 bg-slate-50 p-4">
                  <p class="sm:col-span-2 text-sm font-semibold text-slate-800">Delivery address</p>

                  <label class="block">
                    <span class="form-label">Contact name</span>
                    <input type="text" [(ngModel)]="shipName" name="shipName" class="form-input"
                           placeholder="Leave blank to use the same name" />
                  </label>

                  <label class="block">
                    <span class="form-label">Mobile No</span>
                    <input type="tel" [(ngModel)]="shipMobile" name="shipMobile" maxlength="10" inputmode="numeric"
                           class="form-input" placeholder="Leave blank to use the same number" />
                  </label>

                  <label class="block">
                    <span class="form-label">State</span>
                    <select [(ngModel)]="shipState" name="shipState" class="form-input">
                      <option value="">Same as billing</option>
                      @for (s of states(); track s) { <option [value]="s">{{ s }}</option> }
                    </select>
                  </label>

                  <label class="block">
                    <span class="form-label">City</span>
                    <input type="text" [(ngModel)]="shipCity" name="shipCity" class="form-input" placeholder="City" />
                  </label>

                  <label class="block sm:col-span-2">
                    <span class="form-label">Address <span class="text-red-500">*</span></span>
                    <textarea [(ngModel)]="shipAddress" name="shipAddress" rows="3" class="form-input"
                              placeholder="Where the goods should be delivered"></textarea>
                  </label>
                  @if (shipToDifferent() && !shipAddress().trim()) {
                    <span class="sm:col-span-2 text-xs text-red-600 -mt-2">
                      Enter the delivery address, or untick the box above.
                    </span>
                  }
                </div>
              }
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
                      {{ quote().minOrderAmount | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}
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

              @if (placeError()) {
                <p class="mt-3 text-sm text-red-700 bg-red-50 border border-red-200 rounded px-3 py-2">
                  {{ placeError() }}
                </p>
              }

              <button type="button" (click)="submit()" [disabled]="!canSubmit()"
                      class="mt-4 w-full bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300
                             disabled:cursor-not-allowed text-white font-semibold py-3 rounded-lg transition">
                {{ placing() ? 'Placing your order…' : 'Submit order' }}
              </button>

              <p class="mt-2 text-xs text-slate-500 text-center">
                You will be asked to verify your mobile or email before the order is placed.
              </p>
            </div>
          </div>
        }
      </div>
    </section>

    <app-otp-gate
      [open]="gateOpen()"
      (cancelled)="gateOpen.set(false)"
      (verified)="onVerified()" />
  `,
})
export class OrderFormComponent {
  private readonly quickOrder = inject(QuickOrderService);
  private readonly checkout = inject(QuickOrderCheckoutService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly lineCount = this.quickOrder.lineCount;
  readonly gateOpen = signal(false);
  readonly placing = signal(false);
  readonly placeError = signal<string | null>(null);
  readonly placed = signal<PlacedOrder | null>(null);

  readonly state = signal('');
  readonly city = signal('');
  readonly name = signal('');
  readonly mobile = signal('');
  readonly email = signal('');
  readonly address = signal('');
  readonly businessName = signal('');
  readonly gstin = signal('');

  readonly shipToDifferent = signal(false);
  readonly shipName = signal('');
  readonly shipMobile = signal('');
  readonly shipAddress = signal('');
  readonly shipCity = signal('');
  readonly shipState = signal('');

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

  /**
   * Length only. A GSTIN has a checksum, but rejecting a real number because of a rule
   * implemented slightly wrong here would block a sale — and the number is printed on the
   * bill, not used to compute anything. Blank is fine; it is optional.
   */
  readonly gstinValid = computed(() => {
    const g = this.gstin().trim();
    return g.length === 0 || g.length === 15;
  });

  readonly formErrors = computed(() => {
    const errors: string[] = [];
    if (!this.state()) errors.push('Select your state.');
    if (!this.name().trim()) errors.push('Enter your name.');
    if (!this.mobileValid()) errors.push('Enter a valid 10-digit mobile number.');
    if (!this.address().trim()) errors.push('Enter your billing address.');
    if (!this.gstinValid()) errors.push('A GSTIN is 15 characters.');
    if (this.shipToDifferent() && !this.shipAddress().trim()) {
      errors.push('Enter the delivery address, or untick "deliver to a different address".');
    }
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

  /**
   * Submit → verify identity → place. If the buyer is already signed in the gate is
   * skipped entirely; otherwise it opens and the order is placed the moment it closes
   * successfully. The typed basket is untouched throughout — losing it at the login step
   * would be the single most annoying thing this flow could do.
   */
  submit(): void {
    if (!this.canSubmit()) return;
    if (this.auth.isAuthenticated()) this.place();
    else this.gateOpen.set(true);
  }

  onVerified(): void {
    this.gateOpen.set(false);
    this.place();
  }

  private place(): void {
    this.placing.set(true);
    this.placeError.set(null);

    const lines = this.quickOrder.lines().map((l) => ({
      productId: l.item.productId,
      quantity: l.qty,
    }));

    this.checkout
      .place({
        lines,
        state: this.state(),
        city: this.city(),
        name: this.name(),
        mobile: this.mobile(),
        email: this.email(),
        address: this.address(),
        businessName: this.businessName().trim() || null,
        gstin: this.gstin().trim().toUpperCase() || null,
        shipToDifferent: this.shipToDifferent(),
        shipName: this.shipName().trim() || null,
        shipMobile: this.shipMobile().trim() || null,
        shipAddress: this.shipAddress().trim() || null,
        shipCity: this.shipCity().trim() || null,
        shipState: this.shipState() || null,
      })
      .subscribe({
        next: (result) => {
          this.placing.set(false);
          // Only clear the basket once the server has confirmed the order exists.
          this.quickOrder.clear();
          this.placed.set(result);
          // Long enough to read the order number and amount, short enough that it still
          // reads as one continuous flow rather than a screen the buyer has to act on.
          if (this.isBrowser) {
            setTimeout(() => this.router.navigate(['/order', result.orderId, 'pay']), 1800);
          }
        },
        error: (e) => {
          this.placing.set(false);
          this.placeError.set(e?.error?.message ?? 'We could not place your order. Please try again.');
        },
      });
  }
}
