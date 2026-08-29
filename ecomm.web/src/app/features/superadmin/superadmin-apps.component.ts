import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { AppAdmin, RegisteredApp } from '../../core/models/superadmin.model';

/** Developer dashboard (App Store S6) — register and manage first-party apps. */
@Component({
  selector: 'app-superadmin-apps',
  imports: [FormsModule],
  template: `
    <div class="flex items-center justify-between mb-4">
      <h1 class="text-xl font-bold text-slate-900">Apps</h1>
      <button type="button" (click)="showForm.set(!showForm())" class="btn-primary text-sm">{{ showForm() ? 'Cancel' : 'Register app' }}</button>
    </div>
    @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

    @if (created(); as c) {
      <div class="mb-4 rounded-lg bg-amber-50 border border-amber-200 p-3 text-sm">
        <p class="font-medium text-amber-800">{{ c.name }} registered. Copy the secret now — it won't be shown again.</p>
        <p class="mt-1 font-mono text-xs">client_id: {{ c.clientId }}</p>
        <p class="font-mono text-xs">client_secret: {{ c.clientSecret }}</p>
      </div>
    }

    @if (showForm()) {
      <div class="bg-white border border-slate-200 rounded-xl p-4 mb-6 max-w-2xl">
        <div class="grid sm:grid-cols-2 gap-3">
          <label class="block"><span class="lbl">Name</span><input [(ngModel)]="f.name" class="input" /></label>
          <label class="block"><span class="lbl">Category</span><input [(ngModel)]="f.category" class="input" placeholder="Inventory / Analytics…" /></label>
          <label class="block sm:col-span-2"><span class="lbl">Description</span><input [(ngModel)]="f.description" class="input" /></label>
          <label class="block"><span class="lbl">Redirect URIs (comma-sep)</span><input [(ngModel)]="redirects" class="input" placeholder="https://app.com/callback" /></label>
          <label class="block"><span class="lbl">Embed URL (embedded apps)</span><input [(ngModel)]="f.embedUrl" class="input" /></label>
        </div>
        <div class="mt-3">
          <span class="lbl">Scopes</span>
          <div class="flex flex-wrap gap-3 text-sm">
            @for (s of allScopes; track s) {
              <label class="inline-flex items-center gap-1.5"><input type="checkbox" [checked]="scopes.has(s)" (change)="toggleScope(s)" /> {{ s }}</label>
            }
          </div>
        </div>
        <div class="grid sm:grid-cols-4 gap-3 mt-3 items-end">
          <label class="inline-flex items-center gap-1.5 text-sm"><input type="checkbox" [(ngModel)]="f.isEmbedded" /> Embedded</label>
          <label class="block"><span class="lbl">Price ₹</span><input type="number" [(ngModel)]="f.price" class="input" /></label>
          <label class="block"><span class="lbl">Interval</span><select [(ngModel)]="f.billingInterval" class="input"><option value="once">Once</option><option value="monthly">Monthly</option></select></label>
          <label class="block"><span class="lbl">Rev share %</span><input type="number" [(ngModel)]="f.revenueSharePercent" class="input" /></label>
        </div>
        <button type="button" (click)="register()" [disabled]="busy() || !f.name.trim()" class="btn-primary text-sm mt-4">Register</button>
      </div>
    }

    <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-slate-50 text-slate-500 text-left">
          <tr><th class="px-4 py-2 font-medium">App</th><th class="px-4 py-2 font-medium">Client id</th><th class="px-4 py-2 font-medium">Price</th><th class="px-4 py-2 font-medium">Installs</th><th class="px-4 py-2 font-medium">Status</th><th class="px-4 py-2"></th></tr>
        </thead>
        <tbody>
          @for (a of apps(); track a.id) {
            <tr class="border-t border-slate-100">
              <td class="px-4 py-2"><span class="font-medium text-slate-800">{{ a.name }}</span> @if (a.isFirstParty) { <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-blue-50 text-blue-700">1st-party</span> }<div class="text-xs text-slate-400">{{ a.slug }} · {{ a.category }}</div></td>
              <td class="px-4 py-2 font-mono text-xs text-slate-500">{{ a.clientId }}</td>
              <td class="px-4 py-2">{{ a.price > 0 ? ('₹' + a.price + (a.billingInterval === 'monthly' ? '/mo' : '')) : 'Free' }}</td>
              <td class="px-4 py-2">{{ a.installs }}</td>
              <td class="px-4 py-2"><span class="text-xs px-2 py-0.5 rounded-full" [class]="statusClass(a.status)">{{ a.status }}</span></td>
              <td class="px-4 py-2 text-right">
                @if (a.status === 'listed') { <button type="button" (click)="setStatus(a, 'suspended')" class="text-xs text-red-600 hover:underline">Suspend</button> }
                @else { <button type="button" (click)="setStatus(a, 'listed')" class="text-xs text-green-600 hover:underline">List</button> }
              </td>
            </tr>
          }
          @if (!apps().length) { <tr><td colspan="6" class="py-8 text-center text-slate-400">No apps yet.</td></tr> }
        </tbody>
      </table>
    </div>
  `,
})
export class SuperAdminAppsComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly apps = signal<AppAdmin[]>([]);
  readonly showForm = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly created = signal<RegisteredApp | null>(null);

  readonly allScopes = ['products:read', 'orders:read', 'inventory:read', 'inventory:write'];
  scopes = new Set<string>();
  redirects = '';
  f = { name: '', description: '', iconUrl: '', category: '', isEmbedded: false, embedUrl: '', price: 0, billingInterval: 'once', revenueSharePercent: 0 };

  ngOnInit(): void { this.load(); }
  private load(): void { this.svc.apps().subscribe((a) => this.apps.set(a)); }

  toggleScope(s: string): void { this.scopes.has(s) ? this.scopes.delete(s) : this.scopes.add(s); }

  register(): void {
    this.busy.set(true);
    this.svc.registerApp({
      name: this.f.name.trim(), description: this.f.description.trim() || null, iconUrl: null, category: this.f.category.trim() || null,
      redirectUris: this.redirects.split(',').map((r) => r.trim()).filter(Boolean),
      requestedScopes: [...this.scopes], isEmbedded: this.f.isEmbedded, embedUrl: this.f.embedUrl.trim() || null,
      pricingModel: this.f.price > 0 ? (this.f.billingInterval === 'monthly' ? 'recurring' : 'onetime') : 'free',
      price: Number(this.f.price) || 0, billingInterval: this.f.billingInterval, revenueSharePercent: Number(this.f.revenueSharePercent) || 0,
    }).subscribe({
      next: (c) => { this.busy.set(false); this.created.set(c); this.showForm.set(false); this.load(); },
      error: (e) => { this.busy.set(false); this.message.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not register.'); },
    });
  }

  setStatus(a: AppAdmin, status: string): void {
    this.svc.setAppStatus(a.id, status).subscribe(() => this.load());
  }
  statusClass(s: string): string {
    return { listed: 'bg-green-50 text-green-700', suspended: 'bg-red-50 text-red-700', draft: 'bg-slate-100 text-slate-500' }[s] ?? 'bg-slate-100 text-slate-500';
  }
}
