import { Component, OnChanges, inject, input, signal } from '@angular/core';
import {
  DeviceBreakdown, GeoBreakdown, NewVsReturning, SourceBreakdown, StateBreakdown, TopPage, TrafficPoint, TrafficSummary,
} from '../../../core/models/analytics.model';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { ChartPoint, MiniAreaChartComponent } from '../../../shared/mini-area-chart/mini-area-chart.component';

/**
 * "Traffic" tab content for /admin/analytics — sessions, unique visitors, a trend chart, and
 * device/source/page/geo/new-vs-returning breakdowns. Backed by the first-party PageViews
 * table (Features/Analytics), not a third-party analytics tool.
 */
@Component({
  selector: 'app-admin-traffic',
  imports: [MiniAreaChartComponent],
  template: `
    @if (loading()) {
      <div class="p-8 text-center text-slate-400">Loading…</div>
    } @else {
      <!-- Sessions / unique visitors -->
      <div class="grid grid-cols-2 gap-3 mb-4">
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-xs text-slate-400">Sessions</div>
          <div class="text-2xl font-bold text-slate-900">{{ summary()?.sessions ?? 0 }}</div>
          @if (summary(); as s) { <div class="text-xs mt-0.5" [class]="changeClass(s.sessionsChangePct)">{{ changeLabel(s.sessionsChangePct) }} vs previous period</div> }
        </div>
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-xs text-slate-400">Unique visitors</div>
          <div class="text-2xl font-bold text-slate-900">{{ summary()?.uniqueVisitors ?? 0 }}</div>
          @if (summary(); as s) { <div class="text-xs mt-0.5" [class]="changeClass(s.visitorsChangePct)">{{ changeLabel(s.visitorsChangePct) }} vs previous period</div> }
        </div>
      </div>

      <!-- Sessions over time -->
      <div class="bg-white border border-slate-200 rounded-xl p-4 mb-4">
        <div class="text-xs text-slate-400 mb-2">Sessions over time</div>
        <app-mini-area-chart [points]="chartPoints()" />
      </div>

      <div class="grid sm:grid-cols-2 gap-4 mb-4">
        <!-- Device breakdown -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-xs text-slate-400 mb-3">Sessions by device</div>
          @if (!devices().length) { <p class="text-sm text-slate-400">No data for this range.</p> }
          @for (d of devices(); track d.device) {
            <div class="flex items-center gap-2 mb-1.5 text-sm">
              <span class="w-16 text-slate-600">{{ d.device }}</span>
              <span class="h-2 rounded bg-blue-500/70" [style.width.%]="barPct(d.pct)"></span>
              <span class="text-xs text-slate-500 ml-auto whitespace-nowrap">{{ d.sessions }} ({{ d.pct }}%)</span>
            </div>
          }
        </div>

        <!-- Source breakdown -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-xs text-slate-400 mb-3">Sessions by source</div>
          @if (!sources().length) { <p class="text-sm text-slate-400">No data for this range.</p> }
          @for (s of sources(); track s.source) {
            <div class="flex items-center gap-2 mb-1.5 text-sm">
              <span class="w-28 truncate text-slate-600">{{ s.source }}</span>
              <span class="h-2 rounded bg-blue-500/70" [style.width.%]="barPct(s.pct)"></span>
              <span class="text-xs text-slate-500 ml-auto whitespace-nowrap">{{ s.sessions }} ({{ s.pct }}%)</span>
            </div>
          }
        </div>
      </div>

      <div class="grid sm:grid-cols-2 gap-4 mb-4">
        <!-- Top pages -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-xs text-slate-400 mb-3">Top pages</div>
          @if (!pages().length) { <p class="text-sm text-slate-400">No data for this range.</p> }
          @for (p of pages(); track p.path) {
            <div class="flex items-center justify-between text-sm py-1 border-b border-slate-50 last:border-0">
              <span class="text-slate-700 truncate">{{ p.path }}</span>
              <span class="text-slate-500 text-xs shrink-0 ml-2">{{ p.views }} views</span>
            </div>
          }
        </div>

        <!-- New vs returning -->
        <div class="bg-white border border-slate-200 rounded-xl p-4">
          <div class="text-xs text-slate-400 mb-3">New vs returning visitors</div>
          @if (newVsReturning(); as nr) {
            @if (nr.new + nr.returning === 0) {
              <p class="text-sm text-slate-400">No data for this range.</p>
            } @else {
              <div class="flex items-center gap-2 mb-1.5 text-sm">
                <span class="w-20 text-slate-600">New</span>
                <span class="h-2 rounded bg-blue-500/70" [style.width.%]="barPct(nrPct(nr.new, nr))"></span>
                <span class="text-xs text-slate-500 ml-auto">{{ nr.new }}</span>
              </div>
              <div class="flex items-center gap-2 text-sm">
                <span class="w-20 text-slate-600">Returning</span>
                <span class="h-2 rounded bg-emerald-500/70" [style.width.%]="barPct(nrPct(nr.returning, nr))"></span>
                <span class="text-xs text-slate-500 ml-auto">{{ nr.returning }}</span>
              </div>
            }
          }
        </div>
      </div>

      <!-- Geo breakdowns — only shown once a GeoLite2 database is configured server-side -->
      @if (states().length || geo().length) {
        <div class="grid sm:grid-cols-2 gap-4">
          @if (states().length) {
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <div class="text-xs text-slate-400 mb-3">Sessions by state</div>
              @for (s of states(); track s.country + s.state) {
                <div class="flex items-center justify-between text-sm py-1 border-b border-slate-50 last:border-0">
                  <span class="text-slate-700">{{ s.state }}, {{ s.country }}</span>
                  <span class="text-slate-500 text-xs">{{ s.sessions }}</span>
                </div>
              }
            </div>
          }
          @if (geo().length) {
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <div class="text-xs text-slate-400 mb-3">Sessions by city</div>
              @for (g of geo(); track g.country + g.city) {
                <div class="flex items-center justify-between text-sm py-1 border-b border-slate-50 last:border-0">
                  <span class="text-slate-700">{{ g.city }}, {{ g.country }}</span>
                  <span class="text-slate-500 text-xs">{{ g.sessions }}</span>
                </div>
              }
            </div>
          }
        </div>
      }
    }
  `,
})
export class AdminTrafficComponent implements OnChanges {
  private readonly svc = inject(AnalyticsService);

  readonly from = input.required<string>();
  readonly to = input.required<string>();

  readonly loading = signal(true);
  readonly summary = signal<TrafficSummary | null>(null);
  readonly points = signal<TrafficPoint[]>([]);
  readonly devices = signal<DeviceBreakdown[]>([]);
  readonly sources = signal<SourceBreakdown[]>([]);
  readonly pages = signal<TopPage[]>([]);
  readonly geo = signal<GeoBreakdown[]>([]);
  readonly states = signal<StateBreakdown[]>([]);
  readonly newVsReturning = signal<NewVsReturning | null>(null);

  readonly chartPoints = () => this.points().map((p): ChartPoint => ({ label: p.label, value: p.sessions }));

  ngOnChanges(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    const f = this.from(), t = this.to();
    let pending = 8;
    const done = () => { if (--pending === 0) this.loading.set(false); };

    this.svc.trafficSummary(f, t).subscribe({ next: (r) => { this.summary.set(r); done(); }, error: done });
    this.svc.trafficOverTime(f, t).subscribe({ next: (r) => { this.points.set(r); done(); }, error: done });
    this.svc.trafficByDevice(f, t).subscribe({ next: (r) => { this.devices.set(r); done(); }, error: done });
    this.svc.trafficBySource(f, t).subscribe({ next: (r) => { this.sources.set(r); done(); }, error: done });
    this.svc.topPages(f, t).subscribe({ next: (r) => { this.pages.set(r); done(); }, error: done });
    this.svc.trafficByGeo(f, t).subscribe({ next: (r) => { this.geo.set(r); done(); }, error: done });
    this.svc.trafficByState(f, t).subscribe({ next: (r) => { this.states.set(r); done(); }, error: done });
    this.svc.newVsReturning(f, t).subscribe({ next: (r) => { this.newVsReturning.set(r); done(); }, error: done });
  }

  barPct(pct: number): number {
    return Math.max(2, Math.round(pct));
  }

  nrPct(value: number, nr: NewVsReturning): number {
    const total = nr.new + nr.returning;
    return total === 0 ? 0 : (value / total) * 100;
  }

  changeClass(pct: number): string {
    return pct > 0 ? 'text-emerald-600' : pct < 0 ? 'text-red-500' : 'text-slate-400';
  }

  changeLabel(pct: number): string {
    return `${pct > 0 ? '+' : ''}${pct}%`;
  }
}
