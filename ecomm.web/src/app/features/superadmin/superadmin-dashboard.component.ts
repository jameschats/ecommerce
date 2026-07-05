import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { BlocklistEntry, PlatformRevenue, TenantDetail, TenantSummary } from '../../core/models/superadmin.model';

@Component({
  selector: 'app-superadmin-dashboard',
  imports: [FormsModule, DatePipe, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-50">
      <header class="bg-slate-900 text-white px-6 h-14 flex items-center justify-between">
        <div class="font-bold">WavCommerce · Platform Admin</div>
        <div class="flex items-center gap-4 text-sm">
          <a routerLink="/" class="text-slate-300 hover:text-white">← Store</a>
          <button type="button" (click)="logout()" class="text-slate-300 hover:text-white">Sign out</button>
        </div>
      </header>

      <div class="max-w-6xl mx-auto p-6">
        @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
        @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

        <!-- revenue -->
        @if (revenue(); as r) {
          <div class="grid grid-cols-2 sm:grid-cols-5 gap-3 mb-6">
            <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">MRR</div><div class="text-2xl font-bold text-slate-900">₹{{ r.mrr }}</div></div>
            <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">Stores</div><div class="text-2xl font-bold">{{ r.totalTenants }}</div></div>
            <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">Active</div><div class="text-2xl font-bold text-green-600">{{ r.active }}</div></div>
            <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">Trial</div><div class="text-2xl font-bold text-blue-600">{{ r.trial }}</div></div>
            <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-400">Suspended</div><div class="text-2xl font-bold text-red-500">{{ r.suspended }}</div></div>
          </div>
        }

        <div class="flex gap-2 mb-4 text-sm">
          <button type="button" (click)="tab.set('stores')" [class]="tab()==='stores' ? active : idle">Stores</button>
          <button type="button" (click)="loadBlocklist(); tab.set('blocklist')" [class]="tab()==='blocklist' ? active : idle">Blocklist</button>
        </div>

        @if (tab() === 'stores') {
          <div class="grid lg:grid-cols-2 gap-4">
            <!-- directory -->
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <input [(ngModel)]="search" (ngModelChange)="loadTenants()" placeholder="Search stores…" class="input w-full mb-3" />
              <table class="w-full text-sm">
                <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-1">Store</th><th>Plan</th><th>Standing</th><th></th></tr></thead>
                <tbody>
                  @for (t of tenants(); track t.tenantId) {
                    <tr class="border-b border-slate-100 hover:bg-slate-50 cursor-pointer" (click)="select(t)">
                      <td class="py-2 font-medium text-slate-800">{{ t.name }}<div class="text-xs text-slate-400 font-normal">{{ t.slug }} · {{ t.userCount }} users · {{ t.orderCount }} orders</div></td>
                      <td>{{ t.planName || '—' }}<div class="text-xs text-slate-400">{{ t.subStatus }}</div></td>
                      <td><span class="text-xs px-1.5 py-0.5 rounded" [class]="standingClass(t.standing)">{{ t.standing }}</span>@if (t.suspended) { <span class="text-xs text-red-500 block">suspended</span> }</td>
                      <td class="text-right text-blue-600 text-xs">View →</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <!-- detail -->
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              @if (detail(); as d) {
                <div class="flex items-center justify-between mb-2">
                  <h2 class="font-semibold text-slate-800">{{ d.summary.name }}</h2>
                  <a [href]="storeUrl(d.summary.slug)" target="_blank" class="text-xs text-blue-600 hover:underline">Open store ↗</a>
                </div>
                <p class="text-xs text-slate-400 mb-3">{{ d.summary.slug }} · joined {{ d.summary.createdAt | date:'mediumDate' }}</p>

                <div class="mb-3">
                  <span class="lbl">Standing</span>
                  <div class="flex gap-2 items-center">
                    <select [(ngModel)]="standing" class="input"><option>Good</option><option>Trusted</option><option>Watch</option><option>Flagged</option><option>Blacklisted</option></select>
                    <input [(ngModel)]="standingReason" placeholder="reason (optional)" class="input flex-1" />
                    <button type="button" (click)="saveStanding(d.summary.tenantId)" class="btn-primary text-xs">Save</button>
                  </div>
                </div>

                <div class="flex flex-wrap gap-2 mb-4">
                  <button type="button" (click)="impersonate(d.summary.tenantId, 'view')" class="px-3 py-1.5 rounded-lg border border-slate-300 text-xs hover:bg-slate-50">👁 View as store</button>
                  <button type="button" (click)="impersonate(d.summary.tenantId, 'full')" class="px-3 py-1.5 rounded-lg border border-amber-300 text-amber-700 text-xs hover:bg-amber-50">✎ Full impersonate</button>
                  @if (d.summary.suspended) {
                    <button type="button" (click)="activate(d.summary.tenantId)" class="px-3 py-1.5 rounded-lg border border-green-300 text-green-700 text-xs hover:bg-green-50">Reactivate</button>
                  } @else {
                    <button type="button" (click)="suspend(d.summary.tenantId)" class="px-3 py-1.5 rounded-lg border border-red-300 text-red-600 text-xs hover:bg-red-50">Suspend</button>
                  }
                </div>

                <div class="text-xs text-slate-400 mb-1">Contacts</div>
                @for (c of d.contacts; track c.userId) {
                  <div class="text-sm border-t border-slate-100 py-1.5">
                    <span class="font-medium text-slate-700">{{ c.fullName || '—' }}</span> <span class="text-slate-400">· {{ c.roles }}</span>
                    <div class="text-xs text-slate-400">{{ c.email }}{{ c.phoneNumber ? ' · ' + c.phoneNumber : '' }}{{ c.lastLoginAt ? ' · last login ' + (c.lastLoginAt | date:'short') : '' }}</div>
                  </div>
                }
              } @else {
                <div class="text-slate-400 text-sm p-8 text-center">Select a store to manage it.</div>
              }
            </div>
          </div>
        } @else {
          <!-- blocklist -->
          <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-2xl">
            <div class="grid sm:grid-cols-4 gap-2 mb-3">
              <select [(ngModel)]="blockType" class="input"><option>Email</option><option>Gstin</option><option>Phone</option></select>
              <input [(ngModel)]="blockValue" placeholder="value" class="input sm:col-span-2" />
              <button type="button" (click)="addBlock()" class="btn-primary text-xs">Block</button>
            </div>
            @for (b of blocks(); track b.signupBlocklistId) {
              <div class="flex items-center justify-between text-sm border-t border-slate-100 py-1.5">
                <span><span class="text-slate-400">{{ b.type }}</span> {{ b.value }} <span class="text-xs text-slate-400">{{ b.reason }}</span></span>
                <button type="button" (click)="removeBlock(b.signupBlocklistId)" class="text-red-500 hover:underline text-xs">Remove</button>
              </div>
            }
            @if (!blocks().length) { <div class="text-slate-400 text-sm text-center py-6">Nothing blocked.</div> }
          </div>
        }
      </div>
    </div>
  `,
})
export class SuperAdminDashboardComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  private readonly auth = inject(AuthService);

  readonly active = 'px-3 py-1.5 rounded-lg bg-slate-900 text-white';
  readonly idle = 'px-3 py-1.5 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-50';

  readonly revenue = signal<PlatformRevenue | null>(null);
  readonly tenants = signal<TenantSummary[]>([]);
  readonly detail = signal<TenantDetail | null>(null);
  readonly blocks = signal<BlocklistEntry[]>([]);
  readonly tab = signal<'stores' | 'blocklist'>('stores');
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  search = '';
  standing = 'Good';
  standingReason = '';
  blockType = 'Email';
  blockValue = '';

  ngOnInit(): void {
    this.svc.revenue().subscribe((r) => this.revenue.set(r));
    this.loadTenants();
  }

  loadTenants(): void { this.svc.tenants(this.search).subscribe((t) => this.tenants.set(t)); }
  loadBlocklist(): void { this.svc.blocklist().subscribe((b) => this.blocks.set(b)); }

  select(t: TenantSummary): void {
    this.svc.tenant(t.tenantId).subscribe((d) => { this.detail.set(d); this.standing = d.summary.standing; this.standingReason = d.standingReason ?? ''; });
  }

  saveStanding(id: number): void {
    this.svc.setStanding(id, this.standing, this.standingReason || null).subscribe(() => { this.toast('Standing updated.'); this.reload(id); });
  }
  suspend(id: number): void { this.svc.suspend(id).subscribe(() => { this.toast('Store suspended.'); this.reload(id); }); }
  activate(id: number): void { this.svc.activate(id).subscribe(() => { this.toast('Store reactivated.'); this.reload(id); }); }

  impersonate(id: number, mode: 'view' | 'full'): void {
    this.svc.impersonate(id, mode).subscribe((r) => {
      // hand the short-lived token to the store's own origin via URL fragment
      window.open(`${r.storeUrl}/admin#imp=${encodeURIComponent(r.accessToken)}`, '_blank');
    });
  }

  addBlock(): void {
    if (!this.blockValue.trim()) return;
    this.svc.addBlock(this.blockType, this.blockValue.trim(), null).subscribe(() => { this.blockValue = ''; this.loadBlocklist(); });
  }
  removeBlock(id: number): void { this.svc.removeBlock(id).subscribe(() => this.loadBlocklist()); }

  storeUrl(slug: string | null): string { return slug ? `//${slug}.${location.hostname.split('.').slice(-2).join('.')}` : '#'; }
  standingClass(s: string): string {
    return s === 'Blacklisted' ? 'bg-red-50 text-red-700 border border-red-200'
      : s === 'Flagged' || s === 'Watch' ? 'bg-amber-50 text-amber-700 border border-amber-200'
      : s === 'Trusted' ? 'bg-blue-50 text-blue-700 border border-blue-200'
      : 'bg-green-50 text-green-700 border border-green-200';
  }
  logout(): void { this.auth.logout(); location.href = '/'; }

  private reload(id: number): void { this.loadTenants(); this.svc.tenant(id).subscribe((d) => this.detail.set(d)); this.svc.revenue().subscribe((r) => this.revenue.set(r)); }
  private toast(m: string): void { this.message.set(m); this.error.set(null); setTimeout(() => this.message.set(null), 3000); }
}
