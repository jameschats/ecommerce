import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Menu, MenuItem, NavigationAdminService, UrlRedirect } from '../../../core/services/navigation-admin.service';

@Component({
  selector: 'app-admin-navigation',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Navigation</h1>
      <p class="text-sm text-slate-500 mb-4">Edit your storefront menus and set up URL redirects.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <!-- Menus -->
      <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
        <div class="flex gap-2 mb-4">
          @for (m of menus(); track m.handle) {
            <button type="button" (click)="pick(m)" class="text-sm px-3 py-1.5 rounded-lg border"
              [class]="active()?.handle === m.handle ? 'border-primary bg-primary/5 text-primary font-medium' : 'border-slate-200 text-slate-600 hover:bg-slate-50'">{{ m.title }}</button>
          }
        </div>

        @if (active(); as menu) {
          <div class="divide-y divide-slate-100">
            @for (it of items(); track $index) {
              <div class="flex items-center gap-2 py-2">
                <input class="input flex-1" [(ngModel)]="it.label" placeholder="Label (e.g. Shop)" />
                <input class="input flex-1" [(ngModel)]="it.url" placeholder="/products or https://…" />
                <button type="button" (click)="removeItem($index)" class="text-red-500 px-2">×</button>
              </div>
            }
            @if (!items().length) { <p class="text-sm text-slate-400 py-2">No items yet.</p> }
          </div>
          <div class="flex gap-2 mt-3">
            <button type="button" (click)="addItem()" class="text-sm text-blue-600 hover:underline">+ Add item</button>
            <span class="flex-1"></span>
            <button type="button" (click)="saveMenu()" [disabled]="saving()" class="btn-primary text-sm">{{ saving() ? 'Saving…' : 'Save menu' }}</button>
          </div>
        }
      </div>

      <!-- Redirects -->
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <h2 class="font-semibold text-slate-800 mb-1">URL redirects</h2>
        <p class="text-xs text-slate-400 mb-3">Send visitors from an old path to a new one (e.g. /old-product → /product/new).</p>
        <div class="flex gap-2 mb-3">
          <input class="input flex-1" [(ngModel)]="newFrom" placeholder="/old-path" />
          <span class="self-center text-slate-400">→</span>
          <input class="input flex-1" [(ngModel)]="newTo" placeholder="/new-path" />
          <button type="button" (click)="addRedirect()" class="btn-primary text-sm">Add</button>
        </div>
        <div class="divide-y divide-slate-100">
          @for (r of redirects(); track r.urlRedirectId) {
            <div class="flex items-center justify-between py-2 text-sm">
              <span class="text-slate-600"><span class="font-mono text-slate-800">{{ r.fromPath }}</span> → <span class="font-mono text-slate-800">{{ r.toPath }}</span></span>
              <button type="button" (click)="deleteRedirect(r)" class="text-red-500 hover:underline">Delete</button>
            </div>
          }
          @if (!redirects().length) { <p class="text-sm text-slate-400 py-2">No redirects.</p> }
        </div>
      </div>
    </div>
  `,
})
export class AdminNavigationComponent implements OnInit {
  private readonly api = inject(NavigationAdminService);

  readonly menus = signal<Menu[]>([]);
  readonly active = signal<Menu | null>(null);
  readonly items = signal<MenuItem[]>([]);
  readonly redirects = signal<UrlRedirect[]>([]);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  newFrom = '';
  newTo = '';

  ngOnInit(): void {
    this.api.listMenus().subscribe((m) => { this.menus.set(m); if (m.length) this.pick(m[0]); });
    this.api.listRedirects().subscribe((r) => this.redirects.set(r));
  }

  private toast(m: string): void { this.message.set(m); this.error.set(null); setTimeout(() => this.message.set(null), 2500); }
  private fail(e: unknown): void { this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.'); }

  pick(m: Menu): void { this.active.set(m); this.items.set(m.items.map((i) => ({ label: i.label, url: i.url }))); }
  addItem(): void { this.items.set([...this.items(), { label: '', url: '' }]); }
  removeItem(i: number): void { this.items.set(this.items().filter((_, idx) => idx !== i)); }

  saveMenu(): void {
    const m = this.active(); if (!m) return;
    this.saving.set(true); this.error.set(null);
    this.api.saveMenu(m.handle, this.items().filter((i) => i.label.trim())).subscribe({
      next: () => { this.saving.set(false); this.toast('Menu saved.'); },
      error: (e: unknown) => { this.saving.set(false); this.fail(e); },
    });
  }

  addRedirect(): void {
    if (!this.newFrom.trim() || !this.newTo.trim()) return;
    this.api.createRedirect(this.newFrom.trim(), this.newTo.trim()).subscribe({
      next: (r) => { this.redirects.set([...this.redirects(), r]); this.newFrom = ''; this.newTo = ''; this.toast('Redirect added.'); },
      error: (e: unknown) => this.fail(e),
    });
  }
  deleteRedirect(r: UrlRedirect): void {
    this.api.deleteRedirect(r.urlRedirectId).subscribe({ next: () => this.redirects.set(this.redirects().filter((x) => x.urlRedirectId !== r.urlRedirectId)) });
  }
}
