import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CalendarEntry, Festival, GrowthService } from '../../../core/services/growth.service';

interface Cell { date: Date; iso: string; inMonth: boolean; entries: CalendarEntry[]; isToday: boolean; }

@Component({
  selector: 'app-admin-growth-calendar',
  imports: [RouterLink, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 class="text-xl font-bold text-slate-900">🗓️ Marketing calendar</h1>
          <p class="text-sm text-slate-500">Indian festivals + your scheduled campaigns, so you never miss a moment to sell.</p>
        </div>
        <a routerLink="/admin/growth/campaigns" class="text-sm text-primary hover:underline shrink-0">Campaigns →</a>
      </div>

      @if (locked()) {
        <div class="rounded-xl border border-violet-200 bg-violet-50 p-6 text-center">
          <div class="text-3xl">🗓️</div>
          <h2 class="font-semibold text-violet-900 mt-2">The marketing calendar is a plan upgrade away</h2>
          <a routerLink="/admin/billing" class="inline-block mt-3 btn-primary">See plans</a>
        </div>
      } @else {
        <!-- Upcoming occasions -->
        @if (upcoming().length) {
          <h2 class="font-semibold text-slate-800 mb-2">Coming up</h2>
          <div class="flex gap-3 overflow-x-auto pb-2 mb-6">
            @for (f of upcoming(); track f.id) {
              <div class="shrink-0 w-56 border border-slate-200 rounded-xl p-3 bg-white">
                <div class="text-xs font-medium text-amber-600">{{ leadLabel(f.daysAway) }}</div>
                <div class="text-sm font-semibold text-slate-800 mt-0.5">{{ f.name }}</div>
                <div class="text-xs text-slate-500">{{ f.date | date:'EEE, dd MMM' }} · {{ f.region }}</div>
                @if (f.note) { <div class="text-xs text-slate-400 mt-1 line-clamp-2">{{ f.note }}</div> }
                <a routerLink="/admin/growth/campaigns" [queryParams]="{ goal: f.suggestedGoal, brief: f.name }"
                   class="inline-block mt-2 text-sm text-primary hover:underline">Generate campaign →</a>
              </div>
            }
          </div>
        }

        <!-- Month grid -->
        <div class="flex items-center justify-between mb-3">
          <button type="button" (click)="shiftMonth(-1)" class="px-2 py-1 text-slate-500 hover:text-primary">‹ Prev</button>
          <h2 class="font-semibold text-slate-800">{{ monthStart() | date:'MMMM yyyy' }}</h2>
          <button type="button" (click)="shiftMonth(1)" class="px-2 py-1 text-slate-500 hover:text-primary">Next ›</button>
        </div>

        <div class="grid grid-cols-7 gap-px bg-slate-200 border border-slate-200 rounded-lg overflow-hidden text-sm">
          @for (d of dayNames; track d) {
            <div class="bg-slate-50 text-center text-xs font-medium text-slate-500 py-1.5">{{ d }}</div>
          }
          @for (c of cells(); track c.iso) {
            <div class="bg-white min-h-[84px] p-1.5" [class.opacity-40]="!c.inMonth">
              <div class="text-xs" [class]="c.isToday ? 'font-bold text-primary' : 'text-slate-400'">{{ c.date.getDate() }}</div>
              @for (e of c.entries; track $index) {
                @if (e.kind === 'festival') {
                  <a routerLink="/admin/growth/campaigns" [queryParams]="{ goal: e.suggestedGoal, brief: e.title }"
                     class="block mt-1 text-[11px] leading-tight px-1 py-0.5 rounded bg-amber-100 text-amber-800 truncate hover:bg-amber-200"
                     [title]="e.title">🎉 {{ e.title }}</a>
                } @else {
                  <a [routerLink]="['/admin/growth/library']" [queryParams]="{ campaignId: e.campaignId }"
                     class="block mt-1 text-[11px] leading-tight px-1 py-0.5 rounded truncate"
                     [class]="campaignClass(e.status)" [title]="e.title + ' · ' + (e.subtitle || '')">📣 {{ e.title }}</a>
                }
              }
            </div>
          }
        </div>
        <p class="text-xs text-slate-400 mt-2">Festival dates are indicative — lunar-calendar festivals shift year to year.</p>
      }
    </div>
  `,
})
export class AdminGrowthCalendarComponent implements OnInit {
  private readonly api = inject(GrowthService);

  readonly locked = signal(false);
  readonly upcoming = signal<Festival[]>([]);
  readonly entries = signal<CalendarEntry[]>([]);
  readonly monthStart = signal<Date>(this.firstOfMonth(new Date()));

  readonly dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

  readonly cells = computed<Cell[]>(() => {
    const start = this.monthStart();
    const first = new Date(start);
    const gridStart = new Date(first);
    gridStart.setDate(first.getDate() - first.getDay());   // back to the Sunday
    const todayIso = this.iso(new Date());
    const byDate = new Map<string, CalendarEntry[]>();
    for (const e of this.entries()) {
      const key = e.date.slice(0, 10);
      (byDate.get(key) ?? byDate.set(key, []).get(key)!).push(e);
    }
    const out: Cell[] = [];
    for (let i = 0; i < 42; i++) {
      const d = new Date(gridStart);
      d.setDate(gridStart.getDate() + i);
      const iso = this.iso(d);
      out.push({ date: d, iso, inMonth: d.getMonth() === first.getMonth(), entries: byDate.get(iso) ?? [], isToday: iso === todayIso });
    }
    return out;
  });

  ngOnInit(): void {
    this.api.festivals(120).subscribe({
      next: (f) => this.upcoming.set(f.slice(0, 8)),
      error: (e) => { if (e?.status === 402) this.locked.set(true); },
    });
    this.loadMonth();
  }

  shiftMonth(delta: number): void {
    const d = new Date(this.monthStart());
    d.setMonth(d.getMonth() + delta);
    this.monthStart.set(this.firstOfMonth(d));
    this.loadMonth();
  }

  leadLabel(days: number): string {
    if (days <= 0) return 'Today';
    if (days === 1) return 'Tomorrow';
    if (days < 14) return `In ${days} days`;
    const weeks = Math.round(days / 7);
    return `In ${weeks} week${weeks > 1 ? 's' : ''}`;
  }

  campaignClass(status: string | null): string {
    switch (status) {
      case 'Sent': return 'bg-emerald-100 text-emerald-800 hover:bg-emerald-200';
      case 'Scheduled': return 'bg-blue-100 text-blue-800 hover:bg-blue-200';
      case 'Failed': return 'bg-red-100 text-red-700 hover:bg-red-200';
      default: return 'bg-slate-100 text-slate-700 hover:bg-slate-200';
    }
  }

  private loadMonth(): void {
    const start = this.monthStart();
    const end = new Date(start.getFullYear(), start.getMonth() + 1, 0);
    this.api.calendar(this.iso(start), this.iso(end)).subscribe({ next: (e) => this.entries.set(e), error: () => {} });
  }

  private firstOfMonth(d: Date): Date { return new Date(d.getFullYear(), d.getMonth(), 1); }
  private iso(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
