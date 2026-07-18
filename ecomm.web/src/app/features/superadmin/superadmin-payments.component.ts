import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { PlatformPayment } from '../../core/models/superadmin.model';

/** The PLATFORM's own gateway — how merchants pay us (subscriptions, AI credits). */
@Component({
  selector: 'app-superadmin-payments',
  imports: [FormsModule],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Payments</h1>
    <p class="text-sm text-slate-500 mb-4">How merchants pay <span class="font-medium">the platform</span> — subscriptions and AI credit top-ups.</p>

    @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
    @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

    @if (current(); as c) {
      <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-2xl">
        <div class="flex items-center justify-between mb-3">
          <h2 class="font-semibold text-slate-800">Platform gateway</h2>
          <span class="text-xs px-2 py-0.5 rounded" [class]="c.source === 'console' ? 'bg-blue-50 text-blue-700 border border-blue-200' : 'bg-slate-100 text-slate-500'">
            in force: {{ c.provider }} · from {{ c.source === 'console' ? 'this page' : 'api.env' }}
          </span>
        </div>

        <div class="grid sm:grid-cols-3 gap-2">
          <label class="block"><span class="lbl">Provider</span>
            <select [(ngModel)]="provider" class="input"><option value="Mock">Mock (test)</option><option value="Razorpay">Razorpay</option></select>
          </label>
          <label class="block"><span class="lbl">Razorpay key id</span><input [(ngModel)]="keyId" placeholder="rzp_test_…" class="input" /></label>
          <label class="block"><span class="lbl">Key secret</span><input [(ngModel)]="secret" type="password" [placeholder]="c.hasSecret ? '•••••• (unchanged)' : 'required'" class="input" /></label>
        </div>
        <p class="text-[11px] text-slate-400 mt-1">Leave the secret blank to keep the current one. It's encrypted at rest and never shown again.</p>

        <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary text-xs mt-3">{{ saving() ? 'Saving…' : 'Save' }}</button>

        <div class="mt-4 border-t border-slate-100 pt-3 text-[11px] text-slate-500 space-y-1">
          <p>Saving here <span class="font-medium">overrides</span> the <code>Payments__*</code> values in <code>api.env</code> — no SSH needed to rotate keys.</p>
          <p class="text-amber-700">⚠️ Stores that haven't set up their own payment account fall back to these keys for shopper checkout. Safe with test keys; with live keys, merchant revenue would land in the platform account.</p>
        </div>
      </div>
    }
  `,
})
export class SuperAdminPaymentsComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly current = signal<PlatformPayment | null>(null);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  provider = 'Mock';
  keyId = '';
  secret = '';

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.svc.platformPayment().subscribe((c) => {
      this.current.set(c);
      this.provider = c.provider;
      this.keyId = c.razorpayKeyId ?? '';
      this.secret = '';
    });
  }

  save(): void {
    this.saving.set(true);
    this.error.set(null);
    this.svc.savePlatformPayment({ provider: this.provider, razorpayKeyId: this.keyId.trim() || null, razorpayKeySecret: this.secret.trim() || null }).subscribe({
      next: () => { this.saving.set(false); this.message.set('Payment settings saved.'); setTimeout(() => this.message.set(null), 3000); this.load(); },
      error: (e) => { this.saving.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save.'); },
    });
  }
}
