import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PriceSuggestion, PricingSeasonRule, PricingService, ProductPricingControls } from '../../../core/services/pricing.service';

type Tab = 'queue' | 'controls' | 'seasons';

/** Dynamic Pricing (v4 Phase 5) — approval-mode only, no exceptions. Every suggestion here was
 * computed deterministically (inventory + seasonality signals; demand is neutral until Phase 3
 * Track B's event capture exists) and needs an explicit approve to ever change a price. */
@Component({
  selector: 'app-admin-pricing',
  imports: [FormsModule, DatePipe, DecimalPipe, RouterLink],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Dynamic Pricing</h1>
      <p class="text-sm text-slate-500 mb-6">Rule-based price suggestions from stock and season windows. Nothing changes without your approval.</p>

      @if (locked()) {
        <div class="rounded-xl border border-violet-200 bg-violet-50 p-6 text-center">
          <div class="text-3xl">📈</div>
          <h2 class="font-semibold text-violet-900 mt-2">Dynamic Pricing is a plan upgrade away</h2>
          <a routerLink="/admin/billing" class="inline-block mt-3 btn-primary">See plans</a>
        </div>
      } @else {
        <div class="flex gap-1 mb-4 border-b border-slate-200">
          <button type="button" (click)="tab.set('queue')" class="px-3 py-2 text-sm font-medium border-b-2 -mb-px"
                  [class]="tab() === 'queue' ? 'border-primary text-primary' : 'border-transparent text-slate-500'">
            Suggestions {{ pendingCount() > 0 ? '(' + pendingCount() + ')' : '' }}
          </button>
          <button type="button" (click)="tab.set('controls'); loadControls()" class="px-3 py-2 text-sm font-medium border-b-2 -mb-px"
                  [class]="tab() === 'controls' ? 'border-primary text-primary' : 'border-transparent text-slate-500'">
            Product bounds
          </button>
          <button type="button" (click)="tab.set('seasons'); loadSeasons()" class="px-3 py-2 text-sm font-medium border-b-2 -mb-px"
                  [class]="tab() === 'seasons' ? 'border-primary text-primary' : 'border-transparent text-slate-500'">
            Season rules
          </button>
        </div>

        @if (tab() === 'queue') {
          <div class="flex justify-end mb-3">
            <button type="button" (click)="generateNow()" [disabled]="generating()" class="btn-ghost border border-slate-300 text-sm disabled:opacity-60">
              {{ generating() ? 'Checking…' : 'Check for new suggestions' }}
            </button>
          </div>
          @if (genMessage(); as m) { <p class="text-sm text-emerald-600 mb-3">{{ m }}</p> }

          @if (loadingQueue()) {
            <p class="text-slate-400 text-sm">Loading…</p>
          } @else if (pending().length === 0) {
            <div class="bg-white border border-slate-200 rounded-xl p-8 text-center">
              <div class="text-3xl">✅</div>
              <p class="text-slate-600 font-medium mt-2">Nothing pending</p>
              <p class="text-slate-400 text-sm mt-1">Suggestions appear here once stock or a season rule triggers one.</p>
            </div>
          } @else {
            <div class="space-y-3">
              @for (s of pending(); track s.id) {
                <div class="bg-white border border-slate-200 rounded-xl p-4">
                  <div class="flex items-start justify-between gap-3">
                    <div>
                      <div class="text-sm font-medium text-slate-800">{{ s.productName }}</div>
                      <div class="text-sm mt-0.5">
                        ₹{{ s.oldPrice | number:'1.2-2' }} → <span [class]="s.suggestedPrice > s.oldPrice ? 'text-emerald-600' : 'text-red-600'" class="font-semibold">₹{{ s.suggestedPrice | number:'1.2-2' }}</span>
                      </div>
                      @if (s.reason) { <p class="text-xs text-slate-500 mt-1">{{ s.reason }}</p> }
                      <div class="text-[11px] text-slate-400 mt-1">{{ s.suggestedAt | date:'dd MMM, HH:mm' }}</div>
                    </div>
                    <div class="flex gap-2 shrink-0">
                      <button type="button" (click)="approve(s)" class="btn-primary text-sm px-3 py-1.5">Approve</button>
                      <button type="button" (click)="reject(s)" class="text-sm text-slate-500 hover:text-red-600">Dismiss</button>
                    </div>
                  </div>
                </div>
              }
            </div>
          }
        }

        @if (tab() === 'controls') {
          <div class="bg-white border border-slate-200 rounded-xl p-4 mb-4">
            <h2 class="font-semibold text-slate-800 mb-2">Bulk floor/ceiling</h2>
            <p class="text-xs text-slate-500 mb-3">Writes explicit bounds from each product's current price, right now — not a live rule. Skips locked products.</p>
            <div class="flex flex-wrap items-end gap-3">
              <div><label class="lbl">Floor %</label><input type="number" [(ngModel)]="bulkFloor" name="floor" class="input w-24" /></div>
              <div><label class="lbl">Ceiling %</label><input type="number" [(ngModel)]="bulkCeiling" name="ceiling" class="input w-24" /></div>
              <button type="button" (click)="applyBulkBounds()" [disabled]="bulkBusy()" class="btn-primary disabled:opacity-60">
                {{ bulkBusy() ? 'Applying…' : 'Apply to all products' }}
              </button>
            </div>
            @if (bulkMessage(); as m) { <p class="text-sm text-emerald-600 mt-2">{{ m }}</p> }
          </div>

          @if (loadingControls()) {
            <p class="text-slate-400 text-sm">Loading…</p>
          } @else {
            <div class="bg-white border border-slate-200 rounded-xl divide-y divide-slate-100">
              @for (c of controls(); track c.productId) {
                <div class="p-3 flex items-center gap-3 flex-wrap">
                  <div class="flex-1 min-w-[10rem] text-sm text-slate-800">{{ c.name }}</div>
                  <div class="text-xs text-slate-400">₹{{ c.price | number:'1.2-2' }}</div>
                  <input type="number" [ngModel]="c.minPrice" (ngModelChange)="c.minPrice = $event" placeholder="Floor" class="input w-24 text-sm" />
                  <input type="number" [ngModel]="c.maxPrice" (ngModelChange)="c.maxPrice = $event" placeholder="Ceiling" class="input w-24 text-sm" />
                  <label class="flex items-center gap-1 text-xs text-slate-600">
                    <input type="checkbox" [ngModel]="c.priceLocked" (ngModelChange)="c.priceLocked = $event" name="locked-{{ c.productId }}" /> Locked
                  </label>
                  <button type="button" (click)="saveControls(c)" class="text-sm text-primary hover:underline">Save</button>
                </div>
              }
            </div>
          }
        }

        @if (tab() === 'seasons') {
          <div class="bg-white border border-slate-200 rounded-xl p-4 mb-4">
            <h2 class="font-semibold text-slate-800 mb-2">New season rule</h2>
            <div class="grid sm:grid-cols-2 gap-3 mb-3">
              <div><label class="lbl">Name</label><input [(ngModel)]="ruleName" name="ruleName" class="input" placeholder="Diwali peak" /></div>
              <div><label class="lbl">Bias % (+ up, - down)</label><input type="number" [(ngModel)]="ruleBias" name="ruleBias" class="input" /></div>
              <div><label class="lbl">Start date</label><input type="date" [(ngModel)]="ruleStart" name="ruleStart" class="input" /></div>
              <div><label class="lbl">End date</label><input type="date" [(ngModel)]="ruleEnd" name="ruleEnd" class="input" /></div>
            </div>
            @if (ruleError()) { <p class="text-sm text-red-600 mb-2">{{ ruleError() }}</p> }
            <button type="button" (click)="saveSeasonRule()" [disabled]="ruleBusy()" class="btn-primary disabled:opacity-60">
              {{ ruleBusy() ? 'Saving…' : 'Add rule' }}
            </button>
          </div>

          @if (loadingSeasons()) {
            <p class="text-slate-400 text-sm">Loading…</p>
          } @else if (seasons().length === 0) {
            <p class="text-sm text-slate-400">No season rules yet.</p>
          } @else {
            <div class="space-y-2">
              @for (r of seasons(); track r.id) {
                <div class="bg-white border border-slate-200 rounded-lg p-3 flex items-center justify-between">
                  <div>
                    <div class="text-sm font-medium text-slate-800">{{ r.name }} <span [class]="r.biasPercent >= 0 ? 'text-emerald-600' : 'text-red-600'">({{ r.biasPercent > 0 ? '+' : '' }}{{ r.biasPercent }}%)</span></div>
                    <div class="text-xs text-slate-400">{{ r.startDate }} → {{ r.endDate }}</div>
                  </div>
                  <button type="button" (click)="removeSeasonRule(r)" class="text-sm text-red-600 hover:underline">Delete</button>
                </div>
              }
            </div>
          }
        }
      }
    </div>
  `,
})
export class AdminPricingComponent implements OnInit {
  private readonly api = inject(PricingService);

  readonly locked = signal(false);
  readonly tab = signal<Tab>('queue');

  readonly loadingQueue = signal(true);
  readonly pending = signal<PriceSuggestion[]>([]);
  readonly generating = signal(false);
  readonly genMessage = signal<string | null>(null);

  readonly loadingControls = signal(true);
  readonly controls = signal<ProductPricingControls[]>([]);
  readonly bulkBusy = signal(false);
  readonly bulkMessage = signal<string | null>(null);
  bulkFloor = 10;
  bulkCeiling = 15;

  readonly loadingSeasons = signal(true);
  readonly seasons = signal<PricingSeasonRule[]>([]);
  readonly ruleBusy = signal(false);
  readonly ruleError = signal<string | null>(null);
  ruleName = '';
  ruleBias = 10;
  ruleStart = '';
  ruleEnd = '';

  readonly pendingCount = () => this.pending().length;

  ngOnInit(): void { this.loadQueue(); }

  loadQueue(): void {
    this.loadingQueue.set(true);
    this.api.listSuggestions('Pending').subscribe({
      next: (r) => { this.pending.set(r.items); this.loadingQueue.set(false); },
      error: (e) => { this.loadingQueue.set(false); if (e?.status === 402) this.locked.set(true); },
    });
  }

  generateNow(): void {
    this.generating.set(true);
    this.genMessage.set(null);
    this.api.generateNow().subscribe({
      next: (r) => {
        this.generating.set(false);
        this.genMessage.set(r.generated > 0 ? `${r.generated} new suggestion(s).` : 'No new signals right now.');
        this.loadQueue();
      },
      error: () => this.generating.set(false),
    });
  }

  approve(s: PriceSuggestion): void {
    this.api.approve(s.id).subscribe(() => this.loadQueue());
  }

  reject(s: PriceSuggestion): void {
    this.api.reject(s.id).subscribe(() => this.loadQueue());
  }

  loadControls(): void {
    this.loadingControls.set(true);
    this.api.listControls().subscribe({
      next: (r) => { this.controls.set(r); this.loadingControls.set(false); },
      error: () => this.loadingControls.set(false),
    });
  }

  saveControls(c: ProductPricingControls): void {
    this.api.setControls(c.productId, c.minPrice, c.maxPrice, c.priceLocked).subscribe();
  }

  applyBulkBounds(): void {
    this.bulkBusy.set(true);
    this.bulkMessage.set(null);
    this.api.bulkBounds(null, this.bulkFloor, this.bulkCeiling).subscribe({
      next: (r) => { this.bulkBusy.set(false); this.bulkMessage.set(`Bounds set on ${r.updated} product(s).`); this.loadControls(); },
      error: () => this.bulkBusy.set(false),
    });
  }

  loadSeasons(): void {
    this.loadingSeasons.set(true);
    this.api.listSeasonRules().subscribe({
      next: (r) => { this.seasons.set(r); this.loadingSeasons.set(false); },
      error: () => this.loadingSeasons.set(false),
    });
  }

  saveSeasonRule(): void {
    if (!this.ruleName.trim() || !this.ruleStart || !this.ruleEnd) {
      this.ruleError.set('Name, start date and end date are all required.');
      return;
    }
    this.ruleBusy.set(true);
    this.ruleError.set(null);
    this.api.saveSeasonRule({ name: this.ruleName.trim(), startDate: this.ruleStart, endDate: this.ruleEnd, biasPercent: this.ruleBias, categoryId: null }).subscribe({
      next: () => {
        this.ruleBusy.set(false);
        this.ruleName = ''; this.ruleStart = ''; this.ruleEnd = ''; this.ruleBias = 10;
        this.loadSeasons();
      },
      error: (e) => { this.ruleBusy.set(false); this.ruleError.set(e?.error?.message ?? 'Could not save that.'); },
    });
  }

  removeSeasonRule(r: PricingSeasonRule): void {
    this.api.removeSeasonRule(r.id).subscribe(() => this.loadSeasons());
  }
}
