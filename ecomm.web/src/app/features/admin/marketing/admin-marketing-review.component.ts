import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PlanItem, WeekPlan, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * MS2 sub-step 2 — review & confirm the proposed week. The studio proposes a cheap OUTLINE (no
 * creatives yet); here the merchant edits topics, toggles logo/name, removes items, then confirms.
 * Only after confirm (sub-step 3) are creatives generated and scheduled — so this screen never spends
 * credits. Honours the user's "customize, then confirm, before anything is generated".
 */
@Component({
  selector: 'app-admin-marketing-review',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <div class="flex items-start justify-between gap-3 mb-1">
        <h1 class="text-xl font-bold text-slate-900">Marketing Studio — This week's plan</h1>
        @if (plan()?.status === 'draft') {
          <button type="button" (click)="propose()" [disabled]="busy()" class="text-sm text-teal-700 hover:underline shrink-0">Re-propose</button>
        }
      </div>
      <p class="text-sm text-slate-500 mb-5">Review the proposed posts, tweak anything, then confirm. Nothing is generated until you confirm.</p>

      @if (banner(); as b) {
        <div class="mb-5 rounded-lg px-4 py-2.5 text-sm border"
             [class]="b.ok ? 'bg-green-50 text-green-800 border-green-200' : 'bg-amber-50 text-amber-800 border-amber-200'">{{ b.text }}</div>
      }

      @if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else if (!plan()) {
        <div class="text-sm text-slate-600 border border-dashed border-slate-300 rounded-xl p-6 text-center">
          <p class="mb-3">No plan yet for this week.</p>
          <button type="button" (click)="propose()" [disabled]="busy()" class="btn-primary disabled:opacity-60">
            {{ busy() ? 'Proposing…' : 'Propose this week' }}
          </button>
          <p class="text-xs text-slate-400 mt-3">Uses your <a routerLink="/admin/marketing/plan" class="underline">weekly-plan preferences</a>.</p>
        </div>
      } @else if (plan()!.status === 'confirmed') {
        <div class="rounded-xl border border-green-200 bg-green-50 p-6 text-sm text-green-800">
          <p class="font-medium">This week's plan is confirmed. ✓</p>
          <p class="mt-1">Your text posts have been generated and are scheduled — waiting for your approval before they go live. The scheduler screen (to review, approve and reschedule them) is the next piece we're building.</p>
        </div>
      } @else {
        <div class="space-y-3">
          @for (item of plan()!.items; track item.id) {
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <div class="flex items-center gap-2 mb-2">
                <span class="text-xs font-semibold px-2 py-0.5 rounded-full"
                      [class]="item.type === 'poster' ? 'bg-purple-100 text-purple-700' : 'bg-sky-100 text-sky-700'">{{ item.type }}</span>
                <span class="text-xs text-slate-500">{{ formatSlot(item.scheduledAt) }}</span>
                <button type="button" (click)="remove(item)" class="ml-auto text-slate-400 hover:text-red-600 text-sm" title="Remove">✕</button>
              </div>
              <input [(ngModel)]="item.topic" (blur)="saveItem(item)" [name]="'topic' + item.id"
                     class="input mb-2" placeholder="What's this post about?" />
              @if (item.channels.length) {
                <div class="flex flex-wrap gap-1.5 mb-2">
                  @for (ch of item.channels; track ch) {
                    <span class="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">{{ ch }}</span>
                  }
                </div>
              } @else {
                <p class="text-xs text-amber-600 mb-2">No channel for this type yet — set it in <a routerLink="/admin/marketing/plan" class="underline">preferences</a>.</p>
              }
              <div class="flex gap-4 text-sm text-slate-600">
                <label class="flex items-center gap-1.5"><input type="checkbox" [(ngModel)]="item.includeLogo" (change)="saveItem(item)" [name]="'logo' + item.id" /> Logo</label>
                <label class="flex items-center gap-1.5"><input type="checkbox" [(ngModel)]="item.includeName" (change)="saveItem(item)" [name]="'name' + item.id" /> Name</label>
              </div>
            </div>
          }
        </div>

        <div class="flex items-center gap-3 mt-6">
          <button type="button" (click)="confirm()" [disabled]="busy() || plan()!.items.length === 0" class="btn-primary disabled:opacity-60">
            {{ busy() ? 'Working…' : 'Confirm plan' }}
          </button>
          <button type="button" (click)="discard()" [disabled]="busy()" class="text-sm text-red-600 hover:underline">Discard</button>
        </div>
      }
    </div>
  `,
})
export class AdminMarketingReviewComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);

  readonly plan = signal<WeekPlan | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly banner = signal<{ ok: boolean; text: string } | null>(null);

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    this.api.getCurrentPlan().subscribe({
      next: (p) => { this.plan.set(p); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  propose(): void {
    this.busy.set(true);
    this.banner.set(null);
    this.api.proposePlan().subscribe({
      next: (p) => { this.plan.set(p); this.busy.set(false); },
      error: () => { this.busy.set(false); this.banner.set({ ok: false, text: 'Could not propose a plan.' }); },
    });
  }

  saveItem(item: PlanItem): void {
    this.api.updatePlanItem(item.id, { topic: item.topic, includeLogo: item.includeLogo, includeName: item.includeName }).subscribe();
  }

  remove(item: PlanItem): void {
    this.api.removePlanItem(item.id).subscribe(() => {
      const p = this.plan();
      if (p) this.plan.set({ ...p, items: p.items.filter((i) => i.id !== item.id) });
    });
  }

  confirm(): void {
    const p = this.plan();
    if (!p) return;
    this.busy.set(true);
    this.api.confirmPlan(p.id).subscribe({
      next: (res) => { this.plan.set(res); this.busy.set(false); this.banner.set({ ok: true, text: 'Plan confirmed.' }); },
      error: () => { this.busy.set(false); this.banner.set({ ok: false, text: 'Could not confirm the plan.' }); },
    });
  }

  discard(): void {
    const p = this.plan();
    if (!p) return;
    this.busy.set(true);
    this.api.discardPlan(p.id).subscribe({
      next: () => { this.plan.set(null); this.busy.set(false); },
      error: () => this.busy.set(false),
    });
  }

  formatSlot(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })
      + ', ' + d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  }
}
