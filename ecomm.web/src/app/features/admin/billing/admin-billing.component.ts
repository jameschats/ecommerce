import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, NgZone, OnInit, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { BillingHistory, BillingService, CheckoutSession, Plan, PlatformInvoice, Subscription } from '../../../core/services/billing.service';

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

                @if (s.status !== 'Cancelled' && s.planId && s.monthlyPrice > 0) {
                  @if (s.mandateStatus === 'active') {
                    <p class="text-sm text-green-700 mt-2">🔁 Auto-pay on@if (s.paymentMethodSummary) { · {{ s.paymentMethodSummary }} }@if (s.nextChargeAt && !s.cancelAtPeriodEnd) { · next charge {{ s.nextChargeAt | date:'d MMM y' }} }</p>
                    @if (s.cancelAtPeriodEnd) {
                      <p class="text-xs text-amber-600 mt-1">Auto-pay will stop at the end of this cycle.</p>
                    } @else {
                      <button type="button" (click)="cancelAutoPay()" [disabled]="busy()" class="text-sm text-slate-500 hover:underline mt-1">Turn off auto-pay</button>
                    }
                  } @else if (s.mandateStatus === 'pending') {
                    <p class="text-sm text-amber-600 mt-2">Auto-pay setup started — authorize the mandate to finish.</p>
                    <button type="button" (click)="setupAutoPay(s.planId)" [disabled]="busy()" class="text-sm text-primary hover:underline mt-1">Resume setup</button>
                  } @else {
                    <button type="button" (click)="setupAutoPay(s.planId)" [disabled]="busy()" class="btn-primary text-sm mt-3">Set up auto-pay</button>
                    <p class="text-xs text-slate-400 mt-1">Pay automatically each month — no manual renewals.</p>
                  }
                }
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
              @if (p.introPriceInr != null && p.introMonths) {
                <p class="text-2xl font-bold text-slate-900 mt-2">{{ p.introPriceInr | currency:'INR':'symbol':'1.0-0' }}<span class="text-sm font-normal text-slate-400">/mo</span>
                  <span class="ml-2 text-sm font-normal text-slate-400 line-through">{{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}</span>
                </p>
                <p class="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-1.5 py-0.5 inline-block mt-1">First {{ p.introMonths }} months, then {{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}/mo</p>
              } @else {
                <p class="text-2xl font-bold text-slate-900 mt-2">{{ p.monthlyPrice | currency:'INR':'symbol':'1.0-0' }}<span class="text-sm font-normal text-slate-400">/mo</span></p>
              }
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

        <!-- Tax invoices -->
        @if (invoices().length > 0) {
          <h2 class="font-semibold text-slate-800 mb-3 mt-8">Tax invoices</h2>
          <div class="bg-white border border-slate-200 rounded-xl divide-y divide-slate-100">
            @for (inv of invoices(); track inv.id) {
              <div class="flex items-center justify-between px-4 py-3 text-sm">
                <div>
                  <span class="font-medium text-slate-800">{{ inv.invoiceNumber }}</span>
                  <span class="text-slate-400"> · {{ inv.invoiceDate | date:'d MMM y' }} · {{ inv.totalAmount | currency:'INR':'symbol':'1.0-0' }}</span>
                </div>
                <button type="button" (click)="downloadInvoice(inv)" class="text-primary hover:underline">Download PDF</button>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
})
export class AdminBillingComponent implements OnInit {
  private readonly api = inject(BillingService);
  private readonly zone = inject(NgZone);   // Razorpay callbacks fire outside Angular
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly sub = signal<Subscription | null>(null);
  readonly plans = signal<Plan[]>([]);
  readonly history = signal<BillingHistory[]>([]);
  readonly invoices = signal<PlatformInvoice[]>([]);

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    forkJoin({ sub: this.api.current(), plans: this.api.plans(), history: this.api.history(), invoices: this.api.invoices() }).subscribe({
      next: ({ sub, plans, history, invoices }) => {
        this.sub.set(sub); this.plans.set(plans); this.history.set(history); this.invoices.set(invoices); this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  downloadInvoice(inv: PlatformInvoice): void {
    this.api.invoicePdf(inv.id).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = `invoice-${inv.invoiceNumber.replace(/\//g, '-')}.pdf`;
      document.body.appendChild(a); a.click(); a.remove();
      URL.revokeObjectURL(url);
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
    if (p.monthlyPrice <= 0) {   // free plan — nothing to charge
      this.api.selectPlan(p.planId).subscribe({
        next: (s) => { this.sub.set(s); this.busy.set(false); this.toast(`You're now on the ${s.planName} plan.`); },
        error: () => this.busy.set(false),
      });
      return;
    }
    // Paid plans go through recurring auto-pay setup (card-after-trial model).
    this.setupAutoPay(p.planId);
  }

  /** Set up recurring auto-pay for a plan. Redirects to the gateway's mandate page (Razorpay) or
   *  activates immediately (dev Mock). */
  setupAutoPay(planId: number): void {
    this.busy.set(true); this.message.set(null);
    this.api.setupAutoPay(planId).subscribe({
      next: (r) => {
        if (r.authUrl && typeof window !== 'undefined') { window.location.href = r.authUrl; return; }   // authorize mandate
        this.busy.set(false);
        this.toast(r.active ? 'Auto-pay is on — your plan is active.' : 'Almost there — authorize the mandate to finish.');
        this.load();
      },
      error: (e) => { this.busy.set(false); this.message.set(e?.error?.message ?? 'Could not set up auto-pay.'); },
    });
  }

  cancelAutoPay(): void {
    if (typeof window !== 'undefined' && !window.confirm('Turn off auto-pay? Your plan stays active until the end of the current cycle.')) return;
    this.busy.set(true);
    this.api.cancelAutoPay().subscribe({
      next: () => { this.busy.set(false); this.toast('Auto-pay will stop at the end of your current cycle.'); this.load(); },
      error: () => this.busy.set(false),
    });
  }

  /** Loads Razorpay's widget on demand (browser only) and opens it for this billing cycle. */
  private openRazorpay(s: CheckoutSession): void {
    this.loadRazorpayScript().then(() => {
      const w = window as unknown as { Razorpay: new (o: unknown) => { open: () => void } };
      const rzp = new w.Razorpay({
        key: s.keyId,
        amount: Math.round(s.amount * 100),   // paise
        currency: s.currency,
        order_id: s.gatewayOrderId,
        name: 'WavCommerce',
        description: `${s.planName} plan — 1 month`,
        handler: (res: { razorpay_payment_id: string; razorpay_signature: string }) =>
          this.zone.run(() => this.confirmPayment(s, res.razorpay_payment_id, res.razorpay_signature)),
        modal: { ondismiss: () => this.zone.run(() => this.busy.set(false)) },
      });
      rzp.open();
    }).catch(() => { this.busy.set(false); this.toast('Could not load the payment widget.'); });
  }

  private confirmPayment(s: CheckoutSession, paymentId: string, signature: string): void {
    this.api.confirmCheckout({ planId: s.planId, gatewayOrderId: s.gatewayOrderId, paymentId, signature }).subscribe({
      next: (sub) => { this.sub.set(sub); this.busy.set(false); this.toast(`Payment received — you're on ${sub.planName}.`); this.load(); },
      error: () => { this.busy.set(false); this.toast('Payment could not be verified.'); },
    });
  }

  private loadRazorpayScript(): Promise<void> {
    return new Promise((resolve, reject) => {
      if (typeof document === 'undefined') { reject(new Error('no document')); return; }
      if ((window as unknown as { Razorpay?: unknown }).Razorpay) { resolve(); return; }
      const el = document.createElement('script');
      el.src = 'https://checkout.razorpay.com/v1/checkout.js';
      el.onload = () => resolve();
      el.onerror = () => reject(new Error('script failed'));
      document.head.appendChild(el);
    });
  }

  private toast(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 4000); }
  cancel(): void {
    if (typeof window !== 'undefined' && !window.confirm('Cancel your subscription? Your store may be suspended at the end of the current period.')) return;
    this.busy.set(true); this.message.set(null);
    this.api.cancel().subscribe({
      next: () => { this.busy.set(false); this.message.set('Subscription cancelled.'); this.load(); },
      error: () => this.busy.set(false),
    });
  }
}
