import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CheckoutSettings, CheckoutSettingsService } from '../../../core/services/checkout-settings.service';

@Component({
  selector: 'app-admin-checkout-settings',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Checkout &amp; customer accounts</h1>
      <p class="text-sm text-slate-500 mb-5">How customers check out and what they can do from their account.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else {
        <form (ngSubmit)="save()" class="space-y-5">
          <!-- Checkout -->
          <div class="bg-white border border-slate-200 rounded-xl p-6 space-y-4">
            <h2 class="font-semibold text-slate-800">Checkout</h2>

            <label class="block">
              <span class="lbl">Customer contact method</span>
              <select class="input" [(ngModel)]="form.contactMethod" name="cm">
                <option value="email">Email</option>
                <option value="phone">Phone number</option>
              </select>
              <span class="hint">How you'll reach the customer about their order.</span>
            </label>

            <label class="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" [(ngModel)]="form.requirePhone" name="rp" class="w-4 h-4" />
              <span class="text-sm text-slate-800">Require a phone number at checkout</span>
            </label>

            <label class="block">
              <span class="lbl">Maximum quantity per item</span>
              <input class="input" type="number" min="0" [(ngModel)]="form.itemLimit" name="il" />
              <span class="hint">0 = no limit. Applies to each line in the cart.</span>
            </label>

            <div class="pt-1 border-t border-slate-100">
              <label class="flex items-center gap-3 cursor-pointer pt-3">
                <input type="checkbox" [(ngModel)]="form.tippingEnabled" name="te" class="w-4 h-4" />
                <span class="text-sm text-slate-800">Show a tipping option at checkout</span>
              </label>
              @if (form.tippingEnabled) {
                <label class="block mt-2">
                  <span class="lbl">Tip presets (%)</span>
                  <input class="input" [(ngModel)]="form.tipPresets" name="tp" placeholder="5, 10, 15" />
                  <span class="hint">Comma-separated percentages shown as quick options.</span>
                </label>
              }
            </div>
          </div>

          <!-- Customer accounts -->
          <div class="bg-white border border-slate-200 rounded-xl p-6 space-y-3">
            <h2 class="font-semibold text-slate-800">Customer accounts</h2>
            <label class="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" [(ngModel)]="form.selfServeCancel" name="ssc" class="w-4 h-4" />
              <span><span class="text-sm text-slate-800">Let customers cancel their own orders</span>
                <span class="block text-xs text-slate-400">Before dispatch. Turn off to require contacting you.</span></span>
            </label>
            <label class="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" [(ngModel)]="form.selfServeReturns" name="ssr" class="w-4 h-4" />
              <span><span class="text-sm text-slate-800">Show a "request return" option</span>
                <span class="block text-xs text-slate-400">Surfaces a return request on delivered orders. Full returns workflow coming later.</span></span>
            </label>
          </div>

          <button type="submit" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Save settings' }}</button>
        </form>
      }
    </div>
  `,
  styles: [`.hint { display:block; margin-top:.25rem; font-size:.75rem; color:#94a3b8; }`],
})
export class AdminCheckoutSettingsComponent implements OnInit {
  private readonly api = inject(CheckoutSettingsService);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);

  form: CheckoutSettings = {
    contactMethod: 'email', requirePhone: false, tippingEnabled: false, tipPresets: '',
    itemLimit: 0, selfServeCancel: true, selfServeReturns: false,
  };

  ngOnInit(): void {
    this.api.get().subscribe({ next: (s) => { this.form = s; this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  save(): void {
    this.saving.set(true); this.message.set(null);
    this.api.update(this.form).subscribe({
      next: (s) => { this.form = s; this.saving.set(false); this.message.set('Settings saved.'); setTimeout(() => this.message.set(null), 2500); },
      error: () => this.saving.set(false),
    });
  }
}
