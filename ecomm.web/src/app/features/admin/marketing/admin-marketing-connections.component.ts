import { Component, OnInit, inject, signal } from '@angular/core';
import { SocialConnection, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * MS1 — Marketing Studio social connections. One card per network showing connected / not connected,
 * a Connect button that kicks off the platform's OAuth flow, and Disconnect. WavCommerce registers one
 * app per network centrally; each merchant connects their own account here (plan §3.11). Platforms
 * whose app keys aren't configured yet show "Setup in progress" with Connect disabled.
 */
@Component({
  selector: 'app-admin-marketing-connections',
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Connections</h1>
      <p class="text-sm text-slate-500 mb-5">
        Connect your social accounts so the studio can publish posts, posters and videos for you.
      </p>

      @if (banner(); as b) {
        <div class="mb-5 rounded-lg px-4 py-2.5 text-sm"
             [class]="b.ok ? 'bg-green-50 text-green-800 border border-green-200' : 'bg-amber-50 text-amber-800 border border-amber-200'">
          {{ b.text }}
        </div>
      }

      @if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else {
        <div class="grid sm:grid-cols-2 gap-4">
          @for (c of connections(); track c.platform) {
            <div class="bg-white border border-slate-200 rounded-xl p-4 flex items-start justify-between gap-3">
              <div class="min-w-0">
                <div class="flex items-center gap-2">
                  <span class="h-8 w-8 rounded-lg grid place-items-center text-white text-sm font-bold shrink-0"
                        [style.background]="color(c.platform)">{{ c.displayName.charAt(0) }}</span>
                  <div class="min-w-0">
                    <div class="font-semibold text-slate-900 truncate">{{ c.displayName }}</div>
                    <div class="text-xs" [class]="statusClass(c)">{{ statusLabel(c) }}</div>
                  </div>
                </div>
                @if (c.accountName && c.status === 'connected') {
                  <div class="text-xs text-slate-500 mt-1.5 truncate">{{ c.accountName }}</div>
                }
              </div>

              <div class="shrink-0">
                @if (c.status === 'connected' || c.status === 'expired') {
                  @if (c.status === 'expired') {
                    <button type="button" (click)="connect(c)" [disabled]="busy() === c.platform" class="text-sm mb-1 w-full px-3 py-1.5 rounded-lg border border-slate-300 text-slate-700 hover:bg-slate-50 disabled:opacity-60">Reconnect</button>
                  }
                  <button type="button" (click)="disconnect(c)" [disabled]="busy() === c.platform" class="text-xs text-red-600 hover:underline">Disconnect</button>
                } @else if (c.status === 'not_configured') {
                  <span class="text-xs text-slate-400 italic">Setup in progress</span>
                } @else {
                  <button type="button" (click)="connect(c)" [disabled]="busy() === c.platform" class="btn-primary text-sm disabled:opacity-60">
                    {{ busy() === c.platform ? 'Connecting…' : 'Connect' }}
                  </button>
                }
              </div>
            </div>
          }
        </div>

        <p class="text-xs text-slate-400 mt-5">
          Some networks (Facebook, Instagram) need a short review before publishing is enabled — you can still connect your account now.
          WhatsApp is set up separately through your WhatsApp Business provider.
        </p>
      }
    </div>
  `,
})
export class AdminMarketingConnectionsComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);

  readonly connections = signal<SocialConnection[]>([]);
  readonly loading = signal(true);
  readonly busy = signal<string | null>(null);
  readonly banner = signal<{ ok: boolean; text: string } | null>(null);

  ngOnInit(): void {
    this.readReturnParams();
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listConnections().subscribe({
      next: (c) => { this.connections.set(c); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  /** The OAuth callback bounces back here with ?connected= or ?error=. */
  private readReturnParams(): void {
    if (typeof window === 'undefined') return;
    const params = new URLSearchParams(window.location.search);
    const connected = params.get('connected');
    const error = params.get('error');
    if (connected) this.banner.set({ ok: true, text: `${this.name(connected)} connected successfully.` });
    else if (error) this.banner.set({ ok: false, text: `Couldn't connect ${this.name(error)}. Please try again.` });
    if (connected || error) window.history.replaceState({}, '', window.location.pathname);
  }

  connect(c: SocialConnection): void {
    this.busy.set(c.platform);
    this.api.startConnect(c.platform).subscribe({
      next: (r) => { if (typeof window !== 'undefined') window.location.href = r.authorizeUrl; },
      error: () => { this.busy.set(null); this.banner.set({ ok: false, text: `Couldn't start connecting ${c.displayName}.` }); },
    });
  }

  disconnect(c: SocialConnection): void {
    this.busy.set(c.platform);
    this.api.disconnect(c.platform).subscribe({
      next: () => { this.busy.set(null); this.load(); },
      error: () => this.busy.set(null),
    });
  }

  statusLabel(c: SocialConnection): string {
    switch (c.status) {
      case 'connected': return 'Connected';
      case 'expired': return 'Reconnect needed';
      case 'not_configured': return 'Coming soon';
      default: return 'Not connected';
    }
  }
  statusClass(c: SocialConnection): string {
    switch (c.status) {
      case 'connected': return 'text-green-600';
      case 'expired': return 'text-amber-600';
      case 'not_configured': return 'text-slate-400';
      default: return 'text-slate-500';
    }
  }
  color(platform: string): string {
    const map: Record<string, string> = {
      facebook: '#1877f2', instagram: '#e1306c', linkedin: '#0a66c2',
      pinterest: '#bd081c', youtube: '#ff0000', googleads: '#4285f4', whatsapp: '#25d366',
    };
    return map[platform] ?? '#64748b';
  }
  private name(platform: string): string {
    return this.connections().find((c) => c.platform === platform)?.displayName
      ?? platform.charAt(0).toUpperCase() + platform.slice(1);
  }
}
