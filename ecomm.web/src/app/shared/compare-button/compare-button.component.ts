import { Component, computed, inject, input } from '@angular/core';
import { CompareService } from '../../core/services/compare.service';

/** Toggle for adding/removing a product from the (client-side, anonymous) compare list. */
@Component({
  selector: 'app-compare-button',
  template: `
    <button type="button" (click)="toggle($event)" [disabled]="!active() && compare.isFull()"
      [attr.aria-label]="active() ? 'Remove from compare' : 'Add to compare'"
      [attr.title]="active() ? 'In your compare list' : (compare.isFull() ? 'Compare list full (max 4)' : 'Add to compare')"
      class="grid place-items-center rounded-full bg-white/90 hover:bg-white shadow-sm border border-slate-200 transition disabled:opacity-40 disabled:cursor-not-allowed"
      [style.width.px]="size()" [style.height.px]="size()">
      <svg viewBox="0 0 24 24" [attr.width]="size() * 0.55" [attr.height]="size() * 0.55"
        fill="none" stroke="currentColor" stroke-width="2"
        [class]="active() ? 'text-primary' : 'text-slate-500'">
        <path d="M3 3v18h18"/><path d="M8 17V9"/><path d="M13 17V5"/><path d="M18 17v-4"/>
      </svg>
    </button>
  `,
})
export class CompareButtonComponent {
  readonly compare = inject(CompareService);

  readonly productId = input.required<number>();
  readonly size = input(36);

  readonly active = computed(() => this.compare.has(this.productId()));

  toggle(event: Event): void {
    event.preventDefault();
    event.stopPropagation(); // usually sits inside a card link
    this.compare.toggle(this.productId());
  }
}
