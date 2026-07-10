import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { BillingHistory, BillingService, Plan, Subscription } from '../../../core/services/billing.service';

@Component({
  selector: 'app-admin-billing',
  imports: [CurrencyPipe, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Plan &amp; billing</h1>
      <p class="text-sm text-slate-500 mb-5">Your subscription, upcoming charge and payment history.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else {
        <!-- Current plan -->
        <div class="bg-white border border-slate-200 rounded-xl p-6 mb-6">
          @if (sub(); as s) {
            <div class="flex items-start justify-between gap-4 flex-wrap">
              <div>
                <div class="flex items-center gap-2">
                  <h2 class="text-lg font-semibold text-slate-800">{{ s.planName || 'No plan' }}</h2>
                  <span class="text-xs px-2 py-0.5 rounded-full" [class]="badgeClass(s.status)">{{ statusLabel(s) }}</span>
                </div>
                <p class="text-slate-500 text-sm mt-1">{{ s.monthlyPrice | currency:'INR':'symbol':'1.0-0' }} / month</p>
                @if (nextBillLabel(s); as n) { <p class="text-sm text-slate-600 mt-2">{{ n }}</p> }
              </div>
              @if (s.status !== 'Cancelled' && s.planId) {
                <button type="button" (click)="cancel()" [disabled]="busy()"
                        class="text-sm px-3 py-2 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Cancel subscription</button>
              }
            </div>
          } @else {
            <p class="text-slate-500 text-sm">No subscription yet — choose a plan below to get started.</p>
          }
        </div>

        <!-- Plan chooser -->
        <h2 class="font-semibold text-slate-800 mb-3">Plans</h2>
        <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-8">
          @for (p of plans(); track p.planId) {
            <div class="bg-white border rounded-xl p-5 flex flex-col"
                 [class]="isCurrent(p) ? 'border-blue-400 ring-1 ring-blue-200' : 'border-slate-200'">
              <div class="flex items-center justify-between">
                <h3 class="font-semibold text-slate-800">{{ p.name }}</h3>
                @if (isCurrent(p)) { <span class="text-xs px-2 py-0.5 rounded-full bg-blue-50 text-blue-700">Current</span> }
              </div>
              <p class="text-2xl font-bold text-slate-900 mt-2">{{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}<span class="text-sm font-normal text-slate-400">/mo</span></p>
              <ul class="text-sm text-slate-600 mt-3 space-y-1 flex-1">
                <li>{{ p.maxProducts ? (p.maxProducts + ' products') : 'Unlimited products' }}</li>
                <li>{{ p.maxOrders ? (p.maxOrders + ' orders / mo') : 'Unlimited orders' }}</li>
                <li>{{ p.aiCredits }} AI credits</li>
              </ul>
              @if (!isCurrent(p)) {
                <button type="button" (click)="choose(p)" [disabled]="busy()"
                        class="btn-primary w-full py-2 mt-4">{{ changeVerb(p) }}</button>
              }
            </div>
          }
        </div>

        <!-- Billing history -->
        <h2 class="font-semibold text-slate-800 mb-3">Payment history</h2>
        <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
          @if (history().length === 0) {
            <p class="text-slate-400 text-sm p-5">No charges yet.</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-slate-500 text-left">
                  <tr><th class="px-4 py-2 font-medium">Date</th><th class="px-4 py-2 font-medium">Amount</th>
                    <th class="px-4 py-2 font-medium">Period</th><th class="px-4 py-2 font-medium">Status</th>
                    <th class="px-4 py-2 font-medium">Reference</th></tr>
                </thead>
                <tbody>
                  @for (h of history(); track h.id) {
                    <tr class="border-t border-slate-100">
                      <td class="px-4 py-2">{{ h.billedAt | date:'d MMM y' }}</td>
                      <td class="px-4 py-2">{{ h.amount | currency:'INR':'symbol':'1.0-0' }}</td>
                      <td class="px-4 py-2 text-slate-500">
                        @if (h.periodStart) { {{ h.periodStart | date:'d MMM' }} – {{ h.periodEnd | date:'d MMM y' }} } @else { — }
                      </td>
                      <td class="px-4 py-2"><span class="text-xs px-2 py-0.5 rounded-full bg-green-50 text-green-700">{{ h.status }}</span></td>
                      <td class="px-4 py-2 text-slate-400 font-mono text-xs">{{ h.reference || '—' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminBillingComponent implements OnInit {
  private readonly api = inject(BillingService);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly sub = signal<Subscription | null>(null);
  readonly plans = signal<Plan[]>([]);
  readonly history = signal<BillingHistory[]>([]);

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    forkJoin({ sub: this.api.current(), plans: this.api.plans(), history: this.api.history() }).subscribe({
      next: ({ sub, plans, history }) => { this.sub.set(sub); this.plans.set(plans); this.history.set(history); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  isCurrent(p: Plan): boolean { return this.sub()?.planId === p.planId && this.sub()?.status !== 'Cancelled'; }
  changeVerb(p: Plan): string {
    const cur = this.plans().find((x) => x.planId === this.sub()?.planId);
    if (!cur || this.sub()?.status === 'Cancelled') return 'Choose plan';
    return p.monthlyPrice > cur.monthlyPrice ? 'Upgrade' : 'Switch to this plan';
  }

  statusLabel(s: Subscription): string {
    switch (s.status) {
      case 'Trial': return 'Free trial';
      case 'Active': return 'Active';
      case 'PastDue': return 'Payment due';
      case 'Suspended': return 'Suspended';
      case 'Cancelled': return 'Cancelled';
      default: return s.status;
    }
  }
  badgeClass(status: string): string {
    switch (status) {
      case 'Active': return 'bg-green-50 text-green-700';
      case 'Trial': return 'bg-blue-50 text-blue-700';
      case 'PastDue': return 'bg-amber-50 text-amber-700';
      case 'Suspended': case 'Cancelled': return 'bg-red-50 text-red-700';
      default: return 'bg-slate-100 text-slate-600';
    }
  }
  nextBillLabel(s: Subscription): string | null {
    if (s.status === 'PastDue' && s.graceEndsAt) return `Payment overdue — store access ends ${new Date(s.graceEndsAt).toLocaleDateString()}.`;
    if (s.status === 'Cancelled') return 'Your subscription is cancelled.';
    if (!s.currentPeriodEnd) return s.isInTrial ? 'You are on a free trial.' : null;
    const when = new Date(s.currentPeriodEnd).toLocaleDateString();
    return s.isInTrial ? `Trial ends ${when}.` : `Next charge on ${when}.`;
  }

  choose(p: Plan): void {
    this.busy.set(true); this.message.set(null);
    this.api.selectPlan(p.planId).subscribe({
      next: (s) => { this.sub.set(s); this.busy.set(false); this.message.set(`You're now on the ${s.planName} plan.`); setTimeout(() => this.message.set(null), 3000); },
      error: () => this.busy.set(false),
    });
  }
  cancel(): void {
    if (typeof window !== 'undefined' && !window.confirm('Cancel your subscription? Your store may be suspended at the end of the current period.')) return;
    this.busy.set(true); this.message.set(null);
    this.api.cancel().subscribe({
      next: () => { this.busy.set(false); this.message.set('Subscription cancelled.'); this.load(); },
      error: () => this.busy.set(false),
    });
  }
}
