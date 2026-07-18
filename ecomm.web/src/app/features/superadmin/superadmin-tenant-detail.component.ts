import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { PlanOption, TenantDetail } from '../../core/models/superadmin.model';
import { standingClass } from './superadmin-ui';

/** Deep single-store view: standing, lifecycle actions, subscription, usage/GMV, domain, contacts, recent activity. */
@Component({
  selector: 'app-superadmin-tenant-detail',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <a routerLink="/superadmin/stores" class="text-sm text-slate-500 hover:text-slate-700">← All stores</a>

    @if (message()) { <div class="mt-3 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

    @if (detail(); as d) {
      <div class="flex items-center justify-between mt-2 mb-1">
        <h1 class="text-xl font-bold text-slate-900">{{ d.summary.name }}
          <span class="text-xs align-middle ml-2 px-1.5 py-0.5 rounded" [class]="standingClass(d.summary.standing)">{{ d.summary.standing }}</span>
          @if (d.summary.suspended) { <span class="text-xs align-middle ml-1 text-red-500">· suspended</span> }
          @if (d.offboardedAt) { <span class="text-xs align-middle ml-1 text-red-500">· off-boarded</span> }
        </h1>
        <a [href]="storeUrl(d.summary.slug)" target="_blank" class="text-sm text-blue-600 hover:underline">Open store ↗</a>
      </div>
      <p class="text-xs text-slate-400 mb-5">{{ d.summary.slug }} · joined {{ d.summary.createdAt | date:'mediumDate' }}</p>

      <!-- Usage tiles -->
      <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-5">
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">GMV (paid)</div><div class="text-lg font-bold text-slate-900">₹{{ d.usage.gmv | number:'1.0-0' }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Orders</div><div class="text-lg font-bold">{{ d.usage.orders }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">Products</div><div class="text-lg font-bold">{{ d.usage.products }}</div></div>
        <div class="bg-white border border-slate-200 rounded-xl p-3"><div class="text-xs text-slate-400">AI credits</div><div class="text-lg font-bold">{{ d.usage.aiCreditBalance }}</div></div>
      </div>

      <div class="grid lg:grid-cols-2 gap-4">
        <!-- Standing + actions -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <h2 class="font-semibold text-slate-800 mb-3">Governance</h2>
          <span class="lbl">Standing</span>
          <div class="flex gap-2 items-center mb-4">
            <select [(ngModel)]="standing" class="input"><option>Good</option><option>Trusted</option><option>Watch</option><option>Flagged</option><option>Blacklisted</option></select>
            <input [(ngModel)]="standingReason" placeholder="reason (optional)" class="input flex-1" />
            <button type="button" (click)="saveStanding(d.summary.tenantId)" class="btn-primary text-xs">Save</button>
          </div>
          <div class="flex flex-wrap gap-2">
            <button type="button" (click)="impersonate(d.summary.tenantId, 'view')" class="px-3 py-1.5 rounded-lg border border-slate-300 text-xs hover:bg-slate-50">👁 View as store</button>
            <button type="button" (click)="impersonate(d.summary.tenantId, 'full')" class="px-3 py-1.5 rounded-lg border border-amber-300 text-amber-700 text-xs hover:bg-amber-50">✎ Full impersonate</button>
            @if (d.summary.suspended || d.offboardedAt) {
              <button type="button" (click)="activate(d.summary.tenantId)" class="px-3 py-1.5 rounded-lg border border-green-300 text-green-700 text-xs hover:bg-green-50">Reactivate</button>
            } @else {
              <button type="button" (click)="suspend(d.summary.tenantId)" class="px-3 py-1.5 rounded-lg border border-red-300 text-red-600 text-xs hover:bg-red-50">Suspend</button>
              <button type="button" (click)="offboard(d.summary.tenantId)" class="px-3 py-1.5 rounded-lg border border-red-400 text-red-700 text-xs hover:bg-red-50">Off-board</button>
            }
          </div>
        </div>

        <!-- Plan & trial (admin override — no charge; billing-driven changes come in SA5) -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <h2 class="font-semibold text-slate-800 mb-3">Plan &amp; trial</h2>
          <span class="lbl">Plan</span>
          <div class="flex gap-2 items-center mb-3">
            <select [(ngModel)]="selectedPlanId" class="input flex-1">
              @for (p of plans(); track p.planId) { <option [ngValue]="p.planId">{{ p.name }} — ₹{{ p.monthlyPrice }}@if (!p.isActive) { (inactive) }</option> }
            </select>
            <button type="button" (click)="changePlan(d.summary.tenantId)" class="btn-primary text-xs">Set</button>
          </div>
          <span class="lbl">Trial ends</span>
          <div class="flex gap-2 items-center">
            <input type="date" [(ngModel)]="trialDate" class="input flex-1" />
            <button type="button" (click)="saveTrial(d.summary.tenantId)" class="btn-primary text-xs">Save</button>
            <button type="button" (click)="clearTrial(d.summary.tenantId)" class="px-3 py-1.5 rounded-lg border border-slate-300 text-xs hover:bg-slate-50">Clear</button>
          </div>
          <p class="text-[11px] text-slate-400 mt-1">Comp/override only — no payment is taken.</p>
        </div>

        <!-- Notes & tags -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <h2 class="font-semibold text-slate-800 mb-3">Notes &amp; tags</h2>
          <span class="lbl">Tags</span>
          <div class="flex gap-2 items-center mb-3">
            <input [(ngModel)]="tagsInput" placeholder="vip, wholesale" class="input flex-1" />
            <button type="button" (click)="saveTags(d.summary.tenantId)" class="btn-primary text-xs">Save</button>
          </div>
          <span class="lbl">Add note</span>
          <div class="flex gap-2 items-start">
            <textarea [(ngModel)]="newNote" rows="2" placeholder="Internal, platform-only" class="input flex-1"></textarea>
            <button type="button" (click)="addNote(d.summary.tenantId)" class="btn-primary text-xs">Add</button>
          </div>
          @for (n of d.notes; track n.tenantNoteId) {
            <div class="text-sm border-t border-slate-100 py-1.5">
              <div class="text-slate-700">{{ n.note }}</div>
              <div class="text-[11px] text-slate-400">{{ n.createdAt | date:'short' }}</div>
            </div>
          }
        </div>

        <!-- Subscription + domain -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <h2 class="font-semibold text-slate-800 mb-3">Subscription</h2>
          <dl class="text-sm space-y-1.5">
            <div class="flex justify-between"><dt class="text-slate-500">Plan</dt><dd class="text-slate-800">{{ d.subscription.planName || '—' }}</dd></div>
            <div class="flex justify-between"><dt class="text-slate-500">Status</dt><dd class="text-slate-800">{{ d.subscription.status || '—' }}</dd></div>
            <div class="flex justify-between"><dt class="text-slate-500">Trial ends</dt><dd class="text-slate-800">{{ d.subscription.trialEndsAt ? (d.subscription.trialEndsAt | date:'mediumDate') : '—' }}</dd></div>
            <div class="flex justify-between"><dt class="text-slate-500">Renews</dt><dd class="text-slate-800">{{ d.subscription.currentPeriodEnd ? (d.subscription.currentPeriodEnd | date:'mediumDate') : '—' }}</dd></div>
            <div class="flex justify-between"><dt class="text-slate-500">Razorpay sub</dt><dd class="text-slate-800 truncate max-w-[12rem]">{{ d.subscription.razorpaySubscriptionId || '—' }}</dd></div>
            <div class="flex justify-between border-t border-slate-100 pt-1.5"><dt class="text-slate-500">Custom domain</dt><dd class="text-slate-800">{{ d.customDomain || '—' }} @if (d.customDomain) { <span class="text-xs" [class]="d.customDomainVerified ? 'text-green-600' : 'text-amber-600'">{{ d.customDomainVerified ? '✓ verified' : 'unverified' }}</span> }</dd></div>
          </dl>
        </div>

        <!-- Contacts -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <h2 class="font-semibold text-slate-800 mb-2">Contacts</h2>
          @for (c of d.contacts; track c.userId) {
            <div class="text-sm border-t border-slate-100 py-1.5">
              <span class="font-medium text-slate-700">{{ c.fullName || '—' }}</span> <span class="text-slate-400">· {{ c.roles }}</span>
              <div class="text-xs text-slate-400">{{ c.email }}{{ c.phoneNumber ? ' · ' + c.phoneNumber : '' }}{{ c.lastLoginAt ? ' · last login ' + (c.lastLoginAt | date:'short') : '' }}</div>
            </div>
          }
          @if (!d.contacts.length) { <div class="text-sm text-slate-400 py-2">No users.</div> }
        </div>

        <!-- Recent activity -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <h2 class="font-semibold text-slate-800 mb-2">Recent platform activity</h2>
          @for (a of d.recentActivity; track a.platformAccessLogId) {
            <div class="text-sm border-t border-slate-100 py-1.5 flex justify-between gap-2">
              <span class="text-slate-700">{{ a.action }}<span class="text-slate-400">{{ a.detail ? ' · ' + a.detail : '' }}</span></span>
              <span class="text-xs text-slate-400 whitespace-nowrap">{{ a.createdAt | date:'short' }}</span>
            </div>
          }
          @if (!d.recentActivity.length) { <div class="text-sm text-slate-400 py-2">No activity logged.</div> }
        </div>
      </div>
    } @else {
      <div class="text-slate-400 text-sm p-8 text-center">Loading…</div>
    }
  `,
})
export class SuperAdminTenantDetailComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  private readonly route = inject(ActivatedRoute);

  readonly detail = signal<TenantDetail | null>(null);
  readonly plans = signal<PlanOption[]>([]);
  readonly message = signal<string | null>(null);
  readonly standingClass = standingClass;
  private id = 0;
  standing = 'Good';
  standingReason = '';
  selectedPlanId: number | null = null;
  trialDate = '';
  tagsInput = '';
  newNote = '';

  ngOnInit(): void {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.svc.plans().subscribe((p) => this.plans.set(p));
    this.load();
  }

  private load(): void {
    this.svc.tenant(this.id).subscribe((d) => {
      this.detail.set(d);
      this.standing = d.summary.standing;
      this.standingReason = d.standingReason ?? '';
      this.selectedPlanId = d.subscription.planId ?? null;
      this.trialDate = d.subscription.trialEndsAt ? d.subscription.trialEndsAt.slice(0, 10) : '';
      this.tagsInput = d.tags.join(', ');
    });
  }

  saveStanding(id: number): void { this.svc.setStanding(id, this.standing, this.standingReason || null).subscribe(() => this.after('Standing updated.')); }
  suspend(id: number): void { this.svc.suspend(id).subscribe(() => this.after('Store suspended.')); }
  activate(id: number): void { this.svc.activate(id).subscribe(() => this.after('Store reactivated.')); }
  offboard(id: number): void {
    if (!confirm('Off-board this store? Its storefront goes offline until reactivated.')) return;
    this.svc.offboard(id).subscribe(() => this.after('Store off-boarded.'));
  }
  changePlan(id: number): void {
    if (this.selectedPlanId == null) return;
    this.svc.changePlan(id, this.selectedPlanId).subscribe(() => this.after('Plan changed.'));
  }
  saveTrial(id: number): void {
    this.svc.setTrial(id, this.trialDate ? new Date(this.trialDate).toISOString() : null).subscribe(() => this.after('Trial updated.'));
  }
  clearTrial(id: number): void { this.trialDate = ''; this.svc.setTrial(id, null).subscribe(() => this.after('Trial cleared.')); }
  saveTags(id: number): void { this.svc.setTags(id, this.tagsInput.trim() || null).subscribe(() => this.after('Tags saved.')); }
  addNote(id: number): void {
    if (!this.newNote.trim()) return;
    this.svc.addNote(id, this.newNote.trim()).subscribe(() => { this.newNote = ''; this.after('Note added.'); });
  }

  impersonate(id: number, mode: 'view' | 'full'): void {
    this.svc.impersonate(id, mode).subscribe((r) => {
      window.open(`${r.storeUrl}/admin#imp=${encodeURIComponent(r.accessToken)}`, '_blank');
    });
  }

  storeUrl(slug: string | null): string { return slug ? `//${slug}.${location.hostname.split('.').slice(-2).join('.')}` : '#'; }

  private after(msg: string): void {
    this.message.set(msg);
    setTimeout(() => this.message.set(null), 3000);
    this.load();
  }
}
