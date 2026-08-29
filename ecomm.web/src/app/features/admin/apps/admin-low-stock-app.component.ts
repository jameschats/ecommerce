import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AppStoreService } from '../../../core/services/app-store.service';

/** Config screen for the first-party "Low Stock Alerts" app. */
@Component({
  selector: 'app-admin-low-stock-app',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <a routerLink="/admin/apps" class="text-sm text-primary hover:underline">← App store</a>
      <h1 class="text-xl font-bold text-slate-900 mt-2 mb-1">📦 Low Stock Alerts</h1>
      <p class="text-sm text-slate-500 mb-5">Get an email when a product runs low, so you never miss a restock. Checked twice a day.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (!installed()) {
        <div class="rounded-xl border border-slate-200 bg-white p-6 text-center">
          <p class="text-slate-600">This app isn't installed yet.</p>
          <button type="button" (click)="install()" [disabled]="busy()" class="btn-primary mt-3">Install</button>
        </div>
      } @else {
        <div class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
          <label class="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" [(ngModel)]="enabled" name="enabled" /> Send low-stock alerts
          </label>
          <div>
            <label class="lbl">Alert when stock is at or below</label>
            <input type="number" [(ngModel)]="threshold" name="threshold" min="0" class="input w-32" />
          </div>
          <div>
            <label class="lbl">Send alerts to <span class="text-slate-400 font-normal">(blank = your admin email)</span></label>
            <input type="email" [(ngModel)]="recipientEmail" name="recipient" class="input" placeholder="you@store.com" />
          </div>
          <div class="flex items-center gap-3 pt-1">
            <button type="button" (click)="save()" [disabled]="busy()" class="btn-primary">{{ busy() ? 'Saving…' : 'Save' }}</button>
            <button type="button" (click)="runNow()" [disabled]="busy()" class="btn-ghost border border-slate-300 text-sm">Check now</button>
          </div>
          <button type="button" (click)="uninstall()" [disabled]="busy()" class="text-sm text-red-600 hover:underline">Uninstall app</button>
        </div>
      }
    </div>
  `,
})
export class AdminLowStockAppComponent implements OnInit {
  private readonly api = inject(AppStoreService);
  private readonly slug = 'low-stock-alerts';

  readonly installed = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  private installationId: number | null = null;

  enabled = true;
  threshold = 5;
  recipientEmail = '';

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.api.config(this.slug).subscribe((c) => {
      this.installed.set(c.installed);
      this.installationId = c.installationId;
      if (c.installed) {
        this.enabled = c.settings['enabled'] !== 'false';
        this.threshold = Number(c.settings['threshold']) || 5;
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
      threshold: String(this.threshold || 5),
      recipientEmail: this.recipientEmail.trim() || null,
    }).subscribe({ next: () => { this.busy.set(false); this.toast('Saved.'); }, error: () => this.busy.set(false) });
  }

  runNow(): void {
    this.busy.set(true);
    this.api.runLowStock().subscribe({
      next: (r) => { this.busy.set(false); this.toast(r.lowStockCount > 0 ? `${r.lowStockCount} product(s) low — alert sent.` : 'Nothing is low right now.'); },
      error: () => this.busy.set(false),
    });
  }

  uninstall(): void {
    if (this.installationId == null) return;
    if (typeof window !== 'undefined' && !window.confirm('Uninstall Low Stock Alerts?')) return;
    this.busy.set(true);
    this.api.uninstall(this.installationId).subscribe({ next: () => { this.busy.set(false); this.installed.set(false); this.toast('Uninstalled.'); }, error: () => this.busy.set(false) });
  }

  private toast(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 3000); }
}
