import { Component, computed, input } from '@angular/core';

export interface ChartPoint { label: string; value: number; }

/**
 * Small inline-SVG area/line chart — no charting library in this app, and one chart doesn't
 * justify adding one. `preserveAspectRatio="none"` lets a fixed-size viewBox stretch to
 * whatever width the container gives it via CSS.
 */
@Component({
  selector: 'app-mini-area-chart',
  template: `
    <svg [attr.viewBox]="'0 0 ' + w + ' ' + h" class="w-full h-32" preserveAspectRatio="none" role="img" [attr.aria-label]="ariaLabel()">
      @if (points().length > 1) {
        <path [attr.d]="areaPath()" fill="rgb(37 99 235 / 0.12)" stroke="none" />
        <path [attr.d]="linePath()" fill="none" stroke="rgb(37 99 235)" stroke-width="2" vector-effect="non-scaling-stroke" />
      }
    </svg>
    <div class="flex justify-between text-[10px] text-slate-400 mt-1">
      <span>{{ points()[0]?.label ?? '' }}</span>
      <span>{{ points().at(-1)?.label ?? '' }}</span>
    </div>
  `,
})
export class MiniAreaChartComponent {
  readonly points = input.required<ChartPoint[]>();

  protected readonly w = 600;
  protected readonly h = 160;

  private readonly max = computed(() => Math.max(1, ...this.points().map((p) => p.value)));

  private readonly coords = computed(() => {
    const pts = this.points();
    if (pts.length < 2) return [] as { x: number; y: number }[];
    const max = this.max();
    // 10% headroom so a flat max line doesn't hug the top edge.
    return pts.map((p, i) => ({
      x: (i / (pts.length - 1)) * this.w,
      y: this.h - (p.value / max) * this.h * 0.9,
    }));
  });

  linePath = computed(() =>
    this.coords().map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' '));

  areaPath = computed(() => {
    const c = this.coords();
    if (!c.length) return '';
    return `${this.linePath()} L${this.w},${this.h} L0,${this.h} Z`;
  });

  ariaLabel = computed(() => `Sessions from ${this.points()[0]?.label ?? ''} to ${this.points().at(-1)?.label ?? ''}`);
}
