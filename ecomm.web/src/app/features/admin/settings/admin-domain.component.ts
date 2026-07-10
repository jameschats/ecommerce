import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomainService, DomainStatus } from '../../../core/services/domain.service';

@Component({
  selector: 'app-admin-domain',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Custom domain</h1>
      <p class="text-sm text-slate-500 mb-5">Serve your store on your own domain, e.g. <span class="font-mono">shop.yourbrand.com</span>.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else if (status(); as s) {
        @if (!s.domain) {
          <!-- Connect -->
          <form (ngSubmit)="connect()" class="bg-white border border-slate-200 rounded-xl p-6 space-y-3">
            <label class="block"><span class="lbl">Your domain</span>
              <input class="input" [(ngModel)]="domainInput" name="d" placeholder="shop.yourbrand.com" [disabled]="busy()" /></label>
            <button type="submit" [disabled]="busy() || !domainInput.trim()" class="btn-primary px-5 py-2.5">Connect domain</button>
          </form>
        } @else {
          <!-- Status -->
          <div class="bg-white border border-slate-200 rounded-xl p-6 mb-4">
            <div class="flex items-center justify-between gap-4 flex-wrap">
              <div class="flex items-center gap-2">
                <span class="font-mono text-slate-800">{{ s.domain }}</span>
                @if (s.verified) { <span class="text-xs px-2 py-0.5 rounded-full bg-green-50 text-green-700">Connected</span> }
                @else { <span class="text-xs px-2 py-0.5 rounded-full bg-amber-50 text-amber-700">Pending verification</span> }
              </div>
              <button type="button" (click)="disconnect()" [disabled]="busy()"
                      class="text-sm px-3 py-2 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Disconnect</button>
            </div>
          </div>

          @if (!s.verified) {
            <div class="bg-white border border-slate-200 rounded-xl p-6 space-y-4">
              <h2 class="font-semibold text-slate-800">Point your domain to us</h2>
              <ol class="list-decimal ml-5 space-y-3 text-sm text-slate-600">
                <li>
                  At your DNS provider, add a <strong>CNAME</strong> record:
                  <div class="mt-1 bg-slate-50 border border-slate-200 rounded p-3 font-mono text-xs overflow-x-auto">
                    <div>Host / Name: <span class="text-slate-800">{{ hostPart(s.domain) }}</span></div>
                    <div>Points to: <span class="text-slate-800">{{ s.cnameTarget || 'your platform host' }}</span></div>
                  </div>
                </li>
                <li>DNS can take a few minutes to propagate. Then click <strong>Verify</strong> below — we'll confirm the domain points to us.</li>
              </ol>
              <p class="text-xs text-slate-400">Note: HTTPS certificate provisioning for your domain is handled by us at the edge after verification.</p>
              <button type="button" (click)="verify()" [disabled]="busy()" class="btn-primary px-5 py-2.5">{{ busy() ? 'Verifying…' : 'Verify' }}</button>
            </div>
          } @else {
            <p class="text-sm text-slate-600">Your store is live on <span class="font-mono">{{ s.domain }}</span>.</p>
          }
        }
      }
    </div>
  `,
})
export class AdminDomainComponent implements OnInit {
  private readonly api = inject(DomainService);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly status = signal<DomainStatus | null>(null);
  domainInput = '';

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.get().subscribe({ next: (s) => { this.status.set(s); this.loading.set(false); }, error: () => this.loading.set(false) }); }

  /** CNAME host label — the subdomain part, or "@" for an apex domain. */
  hostPart(domain: string): string {
    const parts = domain.split('.');
    return parts.length > 2 ? parts[0] : '@';
  }

  private run(obs: import('rxjs').Observable<DomainStatus>, ok: string): void {
    this.busy.set(true); this.message.set(null); this.error.set(null);
    obs.subscribe({
      next: (s) => { this.status.set(s); this.busy.set(false); this.message.set(ok); setTimeout(() => this.message.set(null), 3000); },
      error: (e) => { this.busy.set(false); this.error.set(e?.error?.message ?? 'Something went wrong.'); },
    });
  }

  connect(): void { this.run(this.api.connect(this.domainInput.trim()), 'Domain saved — add the DNS record, then verify.'); }
  verify(): void { this.run(this.api.verify(), 'Domain verified — your store is live on it.'); }
  disconnect(): void {
    if (typeof window !== 'undefined' && !window.confirm('Disconnect this domain from your store?')) return;
    this.domainInput = '';
    this.run(this.api.disconnect(), 'Domain disconnected.');
  }
}
