import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AppStoreService } from '../../../core/services/app-store.service';

/** Config screen for the first-party "Sales Digest" app. */
@Component({
  selector: 'app-admin-sales-digest-app',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <a routerLink="/admin/apps" class="text-sm text-primary hover:underline">← App store</a>
      <h1 class="text-xl font-bold text-slate-900 mt-2 mb-1">📈 Sales Digest</h1>
      <p class="text-sm text-slate-500 mb-5">A daily or weekly email summarising your orders, revenue and top products.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (!installed()) {
        <div class="rounded-xl border border-slate-200 bg-white p-6 text-center">
          <p class="text-slate-600">This app isn't installed yet.</p>
          <button type="button" (click)="install()" [disabled]="busy()" class="btn-primary mt-3">Install</button>
        </div>
      } @else {
        <div class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
          <label class="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" [(ngModel)]="enabled" name="enabled" /> Send me the sales digest
          </label>
          <div>
            <label class="lbl">Frequency</label>
            <select [(ngModel)]="frequency" name="frequency" class="input w-40">
              <option value="daily">Daily</option>
              <option value="weekly">Weekly</option>
            </select>
          </div>
          <div>
            <label class="lbl">Send to <span class="text-slate-400 font-normal">(blank = your admin email)</span></label>
            <input type="email" [(ngModel)]="recipientEmail" name="recipient" class="input" placeholder="you@store.com" />
          </div>
          <div class="flex items-center gap-3 pt-1">
            <button type="button" (click)="save()" [disabled]="busy()" class="btn-primary">{{ busy() ? 'Saving…' : 'Save' }}</button>
            <button type="button" (click)="sendNow()" [disabled]="busy()" class="btn-ghost border border-slate-300 text-sm">Send now</button>
          </div>
          <button type="button" (click)="uninstall()" [disabled]="busy()" class="text-sm text-red-600 hover:underline">Uninstall app</button>
        </div>
      }
    </div>
  `,
})
export class AdminSalesDigestAppComponent implements OnInit {
  private readonly api = inject(AppStoreService);
  private readonly slug = 'sales-digest';

  readonly installed = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  private installationId: number | null = null;

  enabled = true;
  frequency = 'daily';
  recipientEmail = '';

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.api.config(this.slug).subscribe((c) => {
      this.installed.set(c.installed);
      this.installationId = c.installationId;
      if (c.installed) {
        this.enabled = c.settings['enabled'] !== 'false';
        this.frequency = c.settings['frequency'] || 'daily';
        this.recipientEmail = c.settings['recipientEmail'] ?? '';
      }
    });
  }

  install(): void {
    this.busy.set(true);
    this.api.install(this.slug).subscribe({ next: () => { this.busy.set(false); this.toast('Installed.'); this.load(); }, error: () => this.busy.set(false) });
  }
  save(): void {
    this.busy.set(true);
    this.api.saveConfig(this.slug, {
      enabled: this.enabled ? 'true' : 'false',
      frequency: this.frequency,
      recipientEmail: this.recipientEmail.trim() || null,
    }).subscribe({ next: () => { this.busy.set(false); this.toast('Saved.'); }, error: () => this.busy.set(false) });
  }
  sendNow(): void {
    this.busy.set(true);
    this.api.runSalesDigest().subscribe({
      next: (r) => { this.busy.set(false); this.toast(`Digest sent (${r.orderCount} order(s) in the window).`); },
      error: () => this.busy.set(false),
    });
  }
  uninstall(): void {
    if (this.installationId == null) return;
    if (typeof window !== 'undefined' && !window.confirm('Uninstall Sales Digest?')) return;
    this.busy.set(true);
    this.api.uninstall(this.installationId).subscribe({ next: () => { this.busy.set(false); this.installed.set(false); this.toast('Uninstalled.'); }, error: () => this.busy.set(false) });
  }
  private toast(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 3000); }
}
