import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ScheduledPost, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * MS2 sub-step 4 — the scheduler. Lists the per-channel posts the plan generated, grouped by day, and
 * lets the merchant approve (the D5 gate), reschedule, skip or delete them. Approved posts publish
 * automatically at their slot via the Hangfire sweep. Also doubles as the job-history view. Publishing
 * needs a connected channel — until one is connected, due posts fail with a clear "connect it" message.
 */
@Component({
  selector: 'app-admin-marketing-scheduler',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-start justify-between gap-3 mb-1">
        <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Scheduler</h1>
        @if (pendingCount() > 0) {
          <button type="button" (click)="approveAll()" [disabled]="busy()" class="btn-primary text-sm shrink-0 disabled:opacity-60">
            Approve all {{ pendingCount() }} pending
          </button>
        }
      </div>
      <p class="text-sm text-slate-500 mb-4">Approve posts to schedule them; they publish automatically at their time. Reschedule, skip or remove any post.</p>

      <div class="flex gap-1.5 mb-5 flex-wrap">
        @for (f of filters; track f.value) {
          <button type="button" (click)="setFilter(f.value)"
                  class="text-xs px-3 py-1 rounded-full border"
                  [class]="filter() === f.value ? 'bg-slate-900 text-white border-slate-900' : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-50'">
            {{ f.label }}
          </button>
        }
      </div>

      @if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else if (posts().length === 0) {
        <div class="text-sm text-slate-600 border border-dashed border-slate-300 rounded-xl p-6 text-center">
          Nothing here yet. Propose and confirm a week on
          <a routerLink="/admin/marketing/plan/review" class="text-teal-700 font-medium hover:underline">This week</a>
          and the posts will appear here to approve.
        </div>
      } @else {
        @for (group of grouped(); track group.day) {
          <h2 class="text-xs font-semibold text-slate-400 uppercase tracking-wide mt-5 mb-2">{{ group.day }}</h2>
          <div class="space-y-2">
            @for (p of group.items; track p.id) {
              <div class="bg-white border border-slate-200 rounded-xl p-4">
                <div class="flex items-center gap-2 mb-1.5">
                  <span class="text-xs font-semibold px-2 py-0.5 rounded-full" [style.background]="chip(p.platform).bg" [style.color]="chip(p.platform).fg">{{ p.platform }}</span>
                  <span class="text-xs px-2 py-0.5 rounded-full" [class]="statusClass(p.status)">{{ statusLabel(p.status) }}</span>
                  <span class="text-xs text-slate-400 ml-auto">{{ time(p.scheduledAt) }}</span>
                </div>
                <div class="flex gap-3">
                  @if (p.mediaUrl && p.type === 'poster') {
                    <img [src]="p.mediaUrl" alt="poster" class="w-20 h-20 rounded-lg border border-slate-200 object-cover shrink-0" />
                  }
                  <div class="min-w-0">
                    <div class="text-sm font-medium text-slate-800">{{ p.topic }}</div>
                    @if (p.preview) { <div class="text-sm text-slate-500 mt-0.5 line-clamp-2">{{ p.preview }}</div> }
                  </div>
                </div>
                @if (p.status === 'failed' && p.error) { <div class="text-xs text-red-600 mt-1">{{ p.error }}</div> }

                <div class="flex items-center gap-3 mt-3 text-sm">
                  @if (p.status === 'pending_approval') {
                    <button type="button" (click)="approve(p)" [disabled]="busy()" class="text-teal-700 font-medium hover:underline">Approve</button>
                  }
                  @if (p.status === 'failed') {
                    <button type="button" (click)="approve(p)" [disabled]="busy()" class="text-teal-700 font-medium hover:underline">Retry</button>
                  }
                  @if (p.status !== 'published') {
                    <label class="text-slate-500 flex items-center gap-1">
                      <input type="datetime-local" [ngModel]="localDt(p.scheduledAt)" (ngModelChange)="reschedule(p, $event)" [name]="'dt' + p.id" class="border border-slate-200 rounded px-1.5 py-0.5 text-xs" />
                    </label>
                    <button type="button" (click)="skip(p)" [disabled]="busy()" class="text-slate-500 hover:underline">Skip</button>
                  }
                  <button type="button" (click)="remove(p)" [disabled]="busy()" class="text-slate-400 hover:text-red-600 ml-auto" title="Delete">✕</button>
                </div>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
})
export class AdminMarketingSchedulerComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);

  readonly posts = signal<ScheduledPost[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly filter = signal<string>('');

  readonly filters = [
    { value: '', label: 'All' },
    { value: 'pending_approval', label: 'Pending' },
    { value: 'scheduled', label: 'Scheduled' },
    { value: 'published', label: 'Published' },
    { value: 'failed', label: 'Failed' },
  ];

  readonly pendingCount = computed(() => this.posts().filter((p) => p.status === 'pending_approval').length);
  readonly grouped = computed(() => {
    const by = new Map<string, ScheduledPost[]>();
    for (const p of this.posts()) {
      const day = new Date(p.scheduledAt).toLocaleDateString(undefined, { weekday: 'long', month: 'short', day: 'numeric' });
      (by.get(day) ?? by.set(day, []).get(day)!).push(p);
    }
    return Array.from(by, ([day, items]) => ({ day, items }));
  });

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    this.api.listScheduled(this.filter() || undefined).subscribe({
      next: (p) => { this.posts.set(p); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  setFilter(v: string): void { this.filter.set(v); this.load(); }

  approve(p: ScheduledPost): void { this.act(this.api.approvePost(p.id)); }
  skip(p: ScheduledPost): void { this.act(this.api.skipPost(p.id)); }
  remove(p: ScheduledPost): void { this.act(this.api.deletePost(p.id)); }
  reschedule(p: ScheduledPost, local: string): void {
    if (!local) return;
    this.act(this.api.reschedulePost(p.id, new Date(local).toISOString()));
  }
  approveAll(): void {
    this.busy.set(true);
    this.api.approveAllPosts().subscribe({ next: () => { this.busy.set(false); this.load(); }, error: () => this.busy.set(false) });
  }

  private act(obs: { subscribe: (o: { next: () => void; error: () => void }) => void }): void {
    this.busy.set(true);
    obs.subscribe({ next: () => { this.busy.set(false); this.load(); }, error: () => this.busy.set(false) });
  }

  statusLabel(s: string): string {
    return { pending_approval: 'Needs approval', scheduled: 'Scheduled', published: 'Published', failed: 'Failed', skipped: 'Skipped' }[s] ?? s;
  }
  statusClass(s: string): string {
    return {
      pending_approval: 'bg-amber-100 text-amber-700', scheduled: 'bg-sky-100 text-sky-700',
      published: 'bg-green-100 text-green-700', failed: 'bg-red-100 text-red-700', skipped: 'bg-slate-100 text-slate-500',
    }[s] ?? 'bg-slate-100 text-slate-600';
  }
  chip(platform: string): { bg: string; fg: string } {
    const map: Record<string, string> = {
      facebook: '#1877f2', instagram: '#e1306c', linkedin: '#0a66c2', pinterest: '#bd081c', youtube: '#ff0000', googleads: '#4285f4', whatsapp: '#25d366',
    };
    return { bg: (map[platform] ?? '#64748b') + '22', fg: map[platform] ?? '#475569' };
  }
  time(iso: string): string { return new Date(iso).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' }); }
  localDt(iso: string): string {
    const d = new Date(iso);
    const pad = (n: number) => `${n}`.padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
