import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { PlatformEmail } from '../../core/models/superadmin.model';

/** Platform transactional email (SMTP) — the shared sender for all stores' order/reset/verify emails.
 * Overrides the api.env `Email` config; per-tenant From-name/Reply-To still layer on top at send time. */
@Component({
  selector: 'app-superadmin-email',
  imports: [FormsModule],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Email (SMTP)</h1>
    <p class="text-sm text-slate-500 mb-4">The platform's shared transactional sender — order confirmations, password resets, verification. Each store's From-name &amp; Reply-To are set per store on top of this.</p>

    @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
    @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

    @if (current(); as c) {
      <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-3xl">
        <div class="flex items-center justify-between mb-1">
          <h2 class="font-semibold text-slate-800">Email (SMTP)</h2>
          <div class="flex items-center gap-2">
            <span class="text-xs px-2 py-0.5 rounded" [class]="c.source === 'console' ? 'bg-blue-50 text-blue-700 border border-blue-200' : 'bg-slate-100 text-slate-500'">
              in force: {{ c.provider === 'Smtp' ? 'Live' : 'Mock' }} · from {{ c.source === 'console' ? 'this page' : 'api.env' }}
            </span>
            <div class="inline-flex rounded-lg border border-slate-300 overflow-hidden text-xs">
              <button type="button" (click)="provider = 'Logging'" class="px-3 py-1" [class]="provider === 'Logging' ? 'bg-slate-800 text-white' : 'text-slate-600 hover:bg-slate-50'">Mock</button>
              <button type="button" (click)="provider = 'Smtp'" class="px-3 py-1 border-l border-slate-300" [class]="provider === 'Smtp' ? 'bg-green-600 text-white' : 'text-slate-600 hover:bg-slate-50'">Live</button>
            </div>
          </div>
        </div>
        <p class="text-[11px] text-slate-400 mb-3">In <b>Mock</b> nothing is sent — messages are written to the server log. Switch to <b>Live</b> once the details below are correct.</p>

        <div class="grid sm:grid-cols-2 gap-3">
          <label class="block"><span class="lbl">SMTP host</span><input [(ngModel)]="host" placeholder="smtp-relay.brevo.com" class="input" /></label>
          <label class="block"><span class="lbl">Port</span><input [(ngModel)]="port" type="number" class="input" /></label>
          <label class="block"><span class="lbl">SMTP login</span><input [(ngModel)]="username" placeholder="xxxx@smtp-brevo.com" class="input" /></label>
          <label class="block"><span class="lbl">SMTP key <span class="text-slate-400 font-normal">{{ c.hasSecret ? '· one is saved' : '' }}</span></span>
            <input [(ngModel)]="password" type="password" [placeholder]="c.hasSecret ? 'Leave blank to keep the saved key' : 'required for Live'" class="input" /></label>
          <label class="block"><span class="lbl">From address</span><input [(ngModel)]="fromAddress" placeholder="no-reply@wavcommerce.online" class="input" />
            <span class="text-[11px] text-slate-400">Must be a sender you have verified in Brevo.</span></label>
          <label class="block"><span class="lbl">From name</span><input [(ngModel)]="fromName" placeholder="WavCommerce" class="input" /></label>
          <label class="flex items-center gap-2 text-sm text-slate-600 mt-1"><input type="checkbox" [(ngModel)]="useSsl" class="rounded border-slate-300 text-primary" /> Use SSL/TLS (STARTTLS)</label>
        </div>
        <p class="text-[11px] text-slate-400 mt-1">Leave the key blank to keep the current one. It's encrypted at rest and never shown again.</p>

        <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary text-xs mt-3">{{ saving() ? 'Saving…' : 'Save' }}</button>

        <div class="mt-4 border-t border-slate-100 pt-3">
          <span class="lbl">Send a test email</span>
          <div class="flex gap-2 mt-1 max-w-md">
            <input [(ngModel)]="testTo" type="email" placeholder="you@example.com" class="input flex-1" />
            <button type="button" (click)="sendTest()" [disabled]="testing() || !testTo.trim()"
              class="shrink-0 rounded-lg border border-slate-300 hover:bg-slate-50 disabled:opacity-50 text-slate-700 text-xs font-medium px-3">
              {{ testing() ? 'Sending…' : 'Send test' }}
            </button>
          </div>
          <p class="text-[11px] text-slate-400 mt-1">Save your changes first — the test uses the saved settings.</p>
        </div>

        <p class="text-[11px] text-amber-700 mt-3">Saving here <span class="font-medium">overrides</span> the <code>Email__*</code> values in <code>api.env</code> — no SSH needed to change SMTP details.</p>
      </div>
    }
  `,
})
export class SuperAdminEmailComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly current = signal<PlatformEmail | null>(null);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  readonly testing = signal(false);

  provider = 'Logging';
  host = '';
  port = 587;
  username = '';
  password = '';
  fromAddress = '';
  fromName = '';
  useSsl = true;
  testTo = '';

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.svc.platformEmail().subscribe((c) => {
      this.current.set(c);
      this.provider = c.provider;
      this.host = c.host ?? '';
      this.port = c.port || 587;
      this.username = c.username ?? '';
      this.password = '';
      this.fromAddress = c.fromAddress ?? '';
      this.fromName = c.fromName ?? '';
      this.useSsl = c.useSsl;
    });
  }

  save(): void {
    this.saving.set(true);
    this.error.set(null);
    this.svc.savePlatformEmail({
      provider: this.provider, host: this.host.trim() || null, port: this.port || 587,
      username: this.username.trim() || null, password: this.password.trim() || null,
      fromAddress: this.fromAddress.trim() || null, fromName: this.fromName.trim() || null, useSsl: this.useSsl,
    }).subscribe({
      next: () => { this.saving.set(false); this.message.set('Email settings saved.'); setTimeout(() => this.message.set(null), 3000); this.load(); },
      error: (e) => { this.saving.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save.'); },
    });
  }

  sendTest(): void {
    this.testing.set(true);
    this.error.set(null);
    this.svc.sendTestEmail(this.testTo.trim()).subscribe({
      next: () => { this.testing.set(false); this.message.set('Test email sent — check the inbox.'); setTimeout(() => this.message.set(null), 4000); },
      error: (e) => { this.testing.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not send test.'); },
    });
  }
}
