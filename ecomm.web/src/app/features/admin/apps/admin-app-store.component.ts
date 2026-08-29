import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppListing, AppStoreService } from '../../../core/services/app-store.service';

/** Merchant App Store — browse and install first-party apps that extend the store. */
@Component({
  selector: 'app-admin-app-store',
  imports: [RouterLink],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">🧩 App store</h1>
      <p class="text-sm text-slate-500 mb-6">Add features to your store. Install with one click; manage or remove any time.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loaded() && apps().length === 0) {
        <p class="text-slate-400">No apps available yet.</p>
      }

      <div class="grid sm:grid-cols-2 gap-4">
        @for (a of apps(); track a.id) {
          <div class="bg-white border border-slate-200 rounded-xl p-4 flex flex-col">
            <div class="flex items-start gap-3">
              <div class="w-10 h-10 rounded-lg bg-slate-100 flex items-center justify-center text-lg shrink-0">🧩</div>
              <div class="min-w-0">
                <div class="flex items-center gap-2">
                  <h2 class="font-semibold text-slate-800">{{ a.name }}</h2>
                  @if (a.installed) { <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-green-100 text-green-700">Installed</span> }
                </div>
                @if (a.category) { <div class="text-xs text-slate-400">{{ a.category }}</div> }
              </div>
            </div>
            @if (a.description) { <p class="text-sm text-slate-600 mt-3 flex-1">{{ a.description }}</p> }
            <div class="text-[11px] text-slate-400 mt-2">Permissions: {{ a.requestedScopes.join(', ') || 'none' }}</div>
            <div class="flex items-center gap-2 mt-4">
              @if (a.installed) {
                <a [routerLink]="['/admin/apps', a.slug]" class="btn-primary text-sm">Open</a>
                <button type="button" (click)="uninstall(a)" [disabled]="busy()" class="text-sm text-red-600 hover:underline">Uninstall</button>
              } @else {
                <button type="button" (click)="install(a)" [disabled]="busy()" class="btn-primary text-sm">Install</button>
              }
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class AdminAppStoreComponent implements OnInit {
  private readonly api = inject(AppStoreService);
  readonly apps = signal<AppListing[]>([]);
  readonly loaded = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.store().subscribe((a) => { this.apps.set(a); this.loaded.set(true); }); }

  install(a: AppListing): void {
    this.busy.set(true);
    this.api.install(a.slug).subscribe({
      next: () => { this.busy.set(false); this.toast(`${a.name} installed.`); this.load(); },
      error: () => this.busy.set(false),
    });
  }
  uninstall(a: AppListing): void {
    if (typeof window !== 'undefined' && !window.confirm(`Uninstall ${a.name}?`)) return;
    this.busy.set(true);
    this.api.installed().subscribe((list) => {
      const inst = list.find((i) => i.slug === a.slug);
      if (!inst) { this.busy.set(false); this.load(); return; }
      this.api.uninstall(inst.installationId).subscribe({
        next: () => { this.busy.set(false); this.toast(`${a.name} uninstalled.`); this.load(); },
        error: () => this.busy.set(false),
      });
    });
  }
  private toast(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 3000); }
}
