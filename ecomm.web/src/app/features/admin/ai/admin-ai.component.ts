import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AiBalance, AiCreditService, AiPack, AiUsage, TopUpResult } from '../../../core/services/ai-credit.service';

// Razorpay checkout widget (loaded on demand; only used when the platform gateway is Razorpay).
declare const Razorpay: new (options: Record<string, unknown>) => { open: () => void };

/**
 * AI credits: balance, buy-credits top-up packs, and the usage ledger. Credits are the metered
 * currency every AI action spends (AI-0 foundation). Top-ups are paid to the PLATFORM — the Mock
 * gateway (dev) grants instantly; a configured Razorpay platform account opens the checkout widget.
 */
@Component({
  selector: 'app-admin-ai',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">AI credits</h1>
      <p class="text-sm text-slate-500 mb-5">Credits power the ✨ AI features across your store. Spend them on descriptions, pages and catalog generation, and top up any time.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (loading()) { <p class="text-slate-400 text-sm">Loading…</p> }
      @else if (bal(); as b) {
        @if (!b.enabled) {
          <div class="mb-5 rounded-lg bg-amber-50 border border-amber-200 text-amber-800 text-sm px-3 py-2">
            AI features aren't switched on for this platform yet. Your balance is safe — you'll be able to spend credits as soon as they're enabled.
          </div>
        }

        <!-- Balance -->
        <div class="bg-white border border-slate-200 rounded-xl p-6 mb-6 flex items-end justify-between gap-4 flex-wrap">
          <div>
            <div class="text-sm text-slate-500">Available balance</div>
            <div class="text-3xl font-bold text-slate-900 mt-1">{{ b.balance }} <span class="text-base font-normal text-slate-400">credits</span></div>
            @if (b.cycleResetAt) { <p class="text-xs text-slate-400 mt-1">Plan credits refresh on {{ b.cycleResetAt | date:'d MMM y' }}.</p> }
          </div>
        </div>

        <!-- Generate catalog CTA -->
        <a routerLink="/admin/ai/catalog" class="block mb-6 rounded-xl border border-violet-200 bg-violet-50 p-4 hover:bg-violet-100 transition">
          <div class="font-medium text-violet-800">✨ Generate a sample catalog</div>
          <p class="text-sm text-violet-700/80 mt-0.5">Create categories and products for a store type in seconds — fully editable.</p>
        </a>

        <!-- Buy credits -->
        <h2 class="font-semibold text-slate-800 mb-3">Buy credits</h2>
        <div class="grid sm:grid-cols-3 gap-4 mb-8">
          @for (p of b.packs; track p.packId) {
            <div class="bg-white border border-slate-200 rounded-xl p-5 flex flex-col">
              <h3 class="font-semibold text-slate-800">{{ p.name }}</h3>
              <p class="text-2xl font-bold text-slate-900 mt-1">{{ p.credits }} <span class="text-sm font-normal text-slate-400">credits</span></p>
              <p class="text-slate-500 text-sm mt-1 flex-1">{{ p.priceInr | currency:'INR':'symbol':'1.0-0' }}</p>
              <button type="button" (click)="buy(p)" [disabled]="busy() !== null"
                      class="btn-primary w-full py-2 mt-3 text-sm">{{ busy() === p.packId ? 'Processing…' : 'Buy' }}</button>
            </div>
          }
        </div>

        <!-- Usage history -->
        <h2 class="font-semibold text-slate-800 mb-3">Recent activity</h2>
        <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
          @if (usage().length === 0) {
            <p class="text-slate-400 text-sm p-5">No AI activity yet.</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-slate-500 text-left">
                  <tr><th class="px-4 py-2 font-medium">When</th><th class="px-4 py-2 font-medium">Action</th>
                    <th class="px-4 py-2 font-medium text-right">Credits</th><th class="px-4 py-2 font-medium text-right">Tokens</th></tr>
                </thead>
                <tbody>
                  @for (u of usage(); track u.id) {
                    <tr class="border-t border-slate-100">
                      <td class="px-4 py-2 text-slate-500">{{ u.createdAt | date:'d MMM y, HH:mm' }}</td>
                      <td class="px-4 py-2">{{ featureLabel(u.feature) }}</td>
                      <td class="px-4 py-2 text-right font-medium" [class]="u.credits < 0 ? 'text-slate-700' : 'text-green-600'">
                        {{ u.credits > 0 ? '+' : '' }}{{ u.credits }}
                      </td>
                      <td class="px-4 py-2 text-right text-slate-400">{{ u.tokens ?? '—' }}</td>
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
export class AdminAiComponent implements OnInit {
  private readonly api = inject(AiCreditService);

  readonly loading = signal(true);
  readonly busy = signal<number | null>(null);   // pack id being purchased
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly bal = signal<AiBalance | null>(null);
  readonly usage = signal<AiUsage[]>([]);

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    forkJoin({ bal: this.api.balance(), usage: this.api.usage() }).subscribe({
      next: ({ bal, usage }) => { this.bal.set(bal); this.usage.set(usage); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private flash(m: string): void { this.message.set(m); setTimeout(() => this.message.set(null), 3000); }

  featureLabel(f: string): string {
    switch (f) {
      case 'improve-text': return 'Improve text';
      case 'seo': return 'SEO copy';
      case 'category': return 'Category text';
      case 'column-map': return 'Import mapping';
      case 'page': return 'Page generation';
      case 'sample-catalog': return 'Sample catalog';
      case 'topup': return 'Credit top-up';
      case 'grant': return 'Plan credits';
      default: return f;
    }
  }

  buy(p: AiPack): void {
    this.busy.set(p.packId); this.error.set(null);
    this.api.topUp(p.packId).subscribe({
      next: (r) => {
        if (r.granted) { this.busy.set(null); this.flash(`${p.credits} credits added.`); this.load(); }
        else this.openRazorpay(p, r);   // platform Razorpay → browser checkout
      },
      error: (e: unknown) => { this.busy.set(null); this.error.set(this.msg(e)); },
    });
  }

  private openRazorpay(p: AiPack, r: TopUpResult): void {
    if (typeof window === 'undefined') { this.busy.set(null); return; }
    this.loadRazorpay().then(() => {
      const rzp = new Razorpay({
        key: r.keyId, amount: r.amountPaise, currency: r.currency ?? 'INR',
        name: 'AI credits', description: `${p.credits} credits`, order_id: r.gatewayOrderId,
        handler: (resp: { razorpay_order_id: string; razorpay_payment_id: string; razorpay_signature: string }) => {
          this.api.verify({
            packId: p.packId, gatewayOrderId: resp.razorpay_order_id,
            gatewayPaymentId: resp.razorpay_payment_id, signature: resp.razorpay_signature,
          }).subscribe({
            next: () => { this.busy.set(null); this.flash(`${p.credits} credits added.`); this.load(); },
            error: (e: unknown) => { this.busy.set(null); this.error.set(this.msg(e)); },
          });
        },
        modal: { ondismiss: () => this.busy.set(null) },
      });
      rzp.open();
    }).catch(() => { this.busy.set(null); this.error.set('Could not load the payment window. Please try again.'); });
  }

  private loadRazorpay(): Promise<void> {
    return new Promise((resolve, reject) => {
      if (typeof Razorpay !== 'undefined') { resolve(); return; }
      const s = document.createElement('script');
      s.src = 'https://checkout.razorpay.com/v1/checkout.js';
      s.onload = () => resolve();
      s.onerror = () => reject();
      document.body.appendChild(s);
    });
  }

  private msg(e: unknown): string { return (e as { error?: { message?: string } })?.error?.message ?? 'Something went wrong. Please try again.'; }
}
