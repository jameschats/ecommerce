import { Component, OnInit, computed, inject, signal } from '@angular/core';
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
        @if (generatedCount() > 0) {
          <div class="rounded-xl border border-green-200 bg-green-50 p-6 text-sm text-green-800 mb-4">
            <p class="font-medium">This week's plan is confirmed. ✓</p>
            <p class="mt-1">
              {{ generatedCount() }} post{{ generatedCount() === 1 ? '' : 's' }} generated and waiting for your approval.
              <a routerLink="/admin/marketing/scheduler" class="underline font-medium">Review them on the Scheduler</a>.
            </p>
          </div>
        } @else {
          <div class="rounded-xl border border-amber-200 bg-amber-50 p-6 text-sm text-amber-800 mb-4">
            <p class="font-medium">Plan confirmed, but nothing was generated yet.</p>
            <p class="mt-1">None of this week's posts have a channel to publish to. <a routerLink="/admin/marketing/connections" class="underline font-medium">Connect a social channel</a>, then come back and try again.</p>
          </div>
        }
        @if (skippedCount() > 0) {
          <div class="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 mb-5 flex items-center justify-between gap-3">
            <span>{{ skippedCount() }} item{{ skippedCount() === 1 ? '' : 's' }} skipped — no channel selected.</span>
            <button type="button" (click)="confirm()" [disabled]="busy()" class="text-amber-900 font-medium underline shrink-0">
              {{ busy() ? 'Retrying…' : 'Try again' }}
            </button>
          </div>
        }
        <div class="space-y-3">
          @for (item of plan()!.items; track item.id) {
            <div class="bg-white border border-slate-200 rounded-xl p-4 flex items-center gap-3">
              <span class="text-xs font-semibold px-2 py-0.5 rounded-full shrink-0"
                    [class]="item.type === 'poster' ? 'bg-purple-100 text-purple-700' : 'bg-sky-100 text-sky-700'">{{ item.type }}</span>
              <span class="text-sm text-slate-700 flex-1 truncate">{{ item.topic }}</span>
              <span class="text-xs px-2 py-0.5 rounded-full shrink-0"
                    [class]="item.status === 'approved' ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'">
                {{ item.status === 'approved' ? 'Generated' : 'Skipped' }}
              </span>
            </div>
          }
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
                <p class="text-xs text-amber-600 mb-2">No channel for this type yet — connect one on <a routerLink="/admin/marketing/connections" class="underline">Connections</a> or set it in <a routerLink="/admin/marketing/plan" class="underline">preferences</a>.</p>
              }
              <div class="flex gap-4 text-sm text-slate-600">
                <label class="flex items-center gap-1.5"><input type="checkbox" [(ngModel)]="item.includeLogo" (change)="saveItem(item)" [name]="'logo' + item.id" /> Logo</label>
                <label class="flex items-center gap-1.5"><input type="checkbox" [(ngModel)]="item.includeName" (change)="saveItem(item)" [name]="'name' + item.id" /> Name</label>
              </div>
            </div>
          }
        </div>

        @if (noChannelCount() === plan()!.items.length && plan()!.items.length > 0) {
          <div class="mt-4 rounded-lg px-4 py-2.5 text-sm bg-amber-50 text-amber-800 border border-amber-200">
            None of these posts have a channel yet — confirming now won't generate anything.
            <a routerLink="/admin/marketing/connections" class="underline font-medium">Connect a social channel first</a>.
          </div>
        } @else if (noChannelCount() > 0) {
          <div class="mt-4 rounded-lg px-4 py-2.5 text-sm bg-amber-50 text-amber-800 border border-amber-200">
            {{ noChannelCount() }} item{{ noChannelCount() === 1 ? '' : 's' }} won't be generated — no channel selected.
          </div>
        }

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

  // Post-confirm outcome, computed from item status/channels rather than a static string — an item
  // stays "proposed" (never "approved") when generation skipped it, almost always for lack of a
  // connected channel. See admin-marketing-review's confirmed-state block.
  readonly generatedCount = computed(() => this.plan()?.items.filter((i) => i.status === 'approved').length ?? 0);
  readonly skippedCount = computed(() => this.plan()?.items.filter((i) => i.status !== 'approved').length ?? 0);
  readonly noChannelCount = computed(() => this.plan()?.items.filter((i) => i.channels.length === 0).length ?? 0);

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
      // The confirmed-state block below renders its own generated/skipped summary from item status,
      // so no banner needed here on success — only on a network/request failure.
      next: ({ plan }) => { this.plan.set(plan); this.busy.set(false); this.banner.set(null); },
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
