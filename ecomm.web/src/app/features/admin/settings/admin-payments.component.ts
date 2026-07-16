import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PaymentAdminService, PaymentSettings } from '../../../core/services/payment-admin.service';

@Component({
  selector: 'app-admin-payments',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Payments</h1>
      <p class="text-sm text-slate-500 mb-5">Choose how customers pay you online, and turn on Cash on Delivery.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else {
        <form (ngSubmit)="save()" class="space-y-5">
          <!-- Online provider -->
          <div class="bg-white border border-slate-200 rounded-xl p-6 space-y-4">
            <h2 class="font-semibold text-slate-800">Online payments</h2>
            <div>
              <label class="lbl">Provider</label>
              <select class="input" [(ngModel)]="form.provider" name="provider">
                <option value="Mock">Built-in simulator (no gateway, no real charges)</option>
                <option value="Razorpay">Razorpay</option>
              </select>
              <p class="text-xs text-slate-400 mt-1">The simulator approves checkouts instantly so you can try the full flow before connecting a gateway.</p>
            </div>

            @if (form.provider === 'Razorpay') {
              <div class="grid gap-4 pt-1">
                <div>
                  <label class="lbl">Razorpay Key ID</label>
                  <input class="input" [(ngModel)]="form.razorpayKeyId" name="keyId" placeholder="rzp_test_… or rzp_live_…" />
                </div>
                <div>
                  <label class="lbl">Razorpay Key Secret</label>
                  <input class="input" type="password" [(ngModel)]="form.razorpayKeySecret" name="keySecret"
                         [placeholder]="hasSecret() ? '•••••••• (saved — leave blank to keep)' : 'Enter your key secret'" />
                  <p class="text-xs text-slate-400 mt-1">Stored encrypted. We never show it again — leave blank to keep the current one.</p>
                </div>
                <label class="flex items-center gap-3 cursor-pointer pt-1">
                  <input type="checkbox" [(ngModel)]="form.isEnabled" name="enabled" class="w-4 h-4" />
                  <span>
                    <span class="text-sm font-medium text-slate-800">Activate Razorpay at checkout</span>
                    <span class="block text-xs text-slate-400">When off, checkout uses the built-in simulator even with keys saved. Test vs real money is decided by your key: <code>rzp_test_</code> keys run Razorpay's test mode (no real charges); <code>rzp_live_</code> keys take real payments.</span>
                  </span>
                </label>
              </div>
            }
          </div>

          <!-- COD -->
          <div class="bg-white border border-slate-200 rounded-xl p-6">
            <h2 class="font-semibold text-slate-800 mb-3">Manual payment methods</h2>
            <label class="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" [(ngModel)]="form.codEnabled" name="cod" class="w-4 h-4" />
              <span>
                <span class="text-sm font-medium text-slate-800">Cash on Delivery (COD)</span>
                <span class="block text-xs text-slate-400">Customers can place orders without paying online and pay on delivery.</span>
              </span>
            </label>
          </div>

          <div class="flex items-center gap-3">
            <button type="submit" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Save payments' }}</button>
          </div>
        </form>
      }
    </div>
  `,
})
export class AdminPaymentsComponent implements OnInit {
  private readonly api = inject(PaymentAdminService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly hasSecret = signal(false);

  form = { provider: 'Mock', razorpayKeyId: '' as string | null, razorpayKeySecret: '' as string | null, isEnabled: false, codEnabled: false };

  ngOnInit(): void { this.load(); }

  private apply(s: PaymentSettings): void {
    this.form = { provider: s.provider, razorpayKeyId: s.razorpayKeyId, razorpayKeySecret: '', isEnabled: s.isEnabled, codEnabled: s.codEnabled };
    this.hasSecret.set(s.hasSecret);
  }

  private load(): void {
    this.api.get().subscribe({ next: (s) => { this.apply(s); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  save(): void {
    this.saving.set(true);
    this.message.set(null);
    this.error.set(null);
    this.api.update(this.form).subscribe({
      next: (s) => { this.apply(s); this.saving.set(false); this.message.set('Payment settings saved.'); setTimeout(() => this.message.set(null), 2500); },
      error: (e: unknown) => { this.saving.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save.'); },
    });
  }
}
