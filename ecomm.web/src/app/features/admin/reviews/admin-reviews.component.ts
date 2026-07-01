import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { AdminReview } from '../../../core/models/review.model';
import { ReviewService } from '../../../core/services/review.service';

@Component({
  selector: 'app-admin-reviews',
  imports: [DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Reviews</h1>
      <p class="text-sm text-slate-500 mb-4">Approve reviews to show them on the storefront. Pending reviews are hidden until approved.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      <div class="flex gap-2 mb-4">
        @for (f of filters; track f.key) {
          <button type="button" (click)="setFilter(f.key)"
            class="px-3 py-1.5 rounded-lg text-sm border"
            [class]="filter() === f.key ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'">
            {{ f.label }}
          </button>
        }
      </div>

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!reviews().length) { <div class="p-8 text-center text-slate-400">No reviews here.</div> }
      @else {
        <div class="space-y-3">
          @for (r of reviews(); track r.reviewId) {
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="text-amber-500 text-sm">{{ stars(r.rating) }}</span>
                    <span class="text-xs font-medium px-1.5 py-0.5 rounded"
                      [class]="r.isApproved ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-amber-50 text-amber-700 border border-amber-200'">
                      {{ r.isApproved ? 'Approved' : 'Pending' }}
                    </span>
                    @if (r.isVerifiedPurchase) { <span class="text-[10px] text-green-700 bg-green-50 border border-green-200 rounded px-1.5 py-0.5">Verified purchase</span> }
                  </div>
                  <p class="font-semibold text-slate-800 mt-1">{{ r.title || '(no title)' }}</p>
                  @if (r.comment) { <p class="text-sm text-slate-600 mt-0.5">{{ r.comment }}</p> }
                  <p class="text-xs text-slate-400 mt-2">{{ r.author }} · {{ r.productName }} · {{ r.createdAt | date: 'dd MMM yyyy' }}</p>
                </div>
                <div class="flex flex-col gap-1.5 shrink-0">
                  @if (!r.isApproved) {
                    <button type="button" (click)="approve(r, true)" class="text-sm text-green-600 hover:underline">Approve</button>
                  } @else {
                    <button type="button" (click)="approve(r, false)" class="text-sm text-amber-600 hover:underline">Unapprove</button>
                  }
                  <button type="button" (click)="remove(r)" class="text-sm text-red-500 hover:underline">Delete</button>
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminReviewsComponent implements OnInit {
  private readonly svc = inject(ReviewService);

  readonly filters = [
    { key: 'pending', label: 'Pending' },
    { key: 'approved', label: 'Approved' },
    { key: '', label: 'All' },
  ];
  readonly reviews = signal<AdminReview[]>([]);
  readonly loading = signal(true);
  readonly filter = signal('pending');
  readonly message = signal<string | null>(null);

  ngOnInit(): void { this.load(); }

  setFilter(key: string): void { this.filter.set(key); this.load(); }

  private load(): void {
    this.loading.set(true);
    this.svc.listAdmin(this.filter() || undefined, 1, 100).subscribe({
      next: (r) => { this.reviews.set(r.items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  stars(n: number): string { return '★'.repeat(n) + '☆'.repeat(5 - n); }

  approve(r: AdminReview, approved: boolean): void {
    this.svc.approve(r.reviewId, approved).subscribe({
      next: () => { this.message.set(approved ? 'Review approved.' : 'Review unapproved.'); this.load(); },
    });
  }

  remove(r: AdminReview): void {
    if (!confirm('Delete this review?')) return;
    this.svc.remove(r.reviewId).subscribe({ next: () => { this.message.set('Review deleted.'); this.load(); } });
  }
}
