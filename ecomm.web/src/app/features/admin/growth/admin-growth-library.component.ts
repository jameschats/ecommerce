import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GrowthContent, GrowthService } from '../../../core/services/growth.service';
import { PagedResult } from '../../../core/models/api-response.model';

@Component({
  selector: 'app-admin-growth-library',
  imports: [DatePipe, RouterLink],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between gap-4 mb-6">
        <div>
          <h1 class="text-xl font-bold text-slate-900">Content library</h1>
          <p class="text-sm text-slate-500">Everything you've generated. Copy, reuse or clean up.</p>
        </div>
        <a routerLink="/admin/growth" class="btn-primary shrink-0">✨ Generate</a>
      </div>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if ((result()?.items ?? []).length === 0) {
        <div class="bg-white border border-slate-200 rounded-xl p-8 text-center">
          <div class="text-3xl">🗂️</div>
          <p class="text-slate-600 font-medium mt-2">Nothing generated yet</p>
          <a routerLink="/admin/growth" class="text-sm text-primary hover:underline mt-1 inline-block">Generate your first post →</a>
        </div>
      } @else {
        <div class="space-y-3">
          @for (c of result()?.items ?? []; track c.id) {
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <span class="text-[11px] px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">{{ label(c.contentType) }}</span>
                  <span class="text-xs text-slate-400 ml-2">{{ c.language }} · {{ c.createdAt | date:'dd MMM, HH:mm' }}</span>
                </div>
                <div class="flex gap-2 shrink-0">
                  <button type="button" (click)="copy(c)" class="text-sm text-primary hover:underline">{{ copiedId() === c.id ? 'Copied ✓' : 'Copy' }}</button>
                  <button type="button" (click)="remove(c)" class="text-sm text-red-600 hover:underline">Delete</button>
                </div>
              </div>
              @if (c.title) { <div class="text-sm font-medium text-slate-800 mt-2">{{ c.title }}</div> }
              <p class="text-sm text-slate-600 mt-1 whitespace-pre-line">{{ c.body }}</p>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminGrowthLibraryComponent implements OnInit {
  private readonly api = inject(GrowthService);

  readonly loading = signal(true);
  readonly result = signal<PagedResult<GrowthContent> | null>(null);
  readonly copiedId = signal<number | null>(null);

  private readonly labels: Record<string, string> = {
    'instagram-caption': 'Instagram', 'facebook-post': 'Facebook', 'whatsapp': 'WhatsApp',
    'email': 'Email', 'product-description': 'Product', 'festival-offer': 'Festival', 'google-ads': 'Google Ads',
  };

  ngOnInit(): void { this.load(); }

  label(key: string): string { return this.labels[key] ?? key; }

  copy(c: GrowthContent): void {
    const text = (c.title ? c.title + '\n\n' : '') + c.body;
    navigator.clipboard?.writeText(text).then(() => {
      this.copiedId.set(c.id);
      setTimeout(() => this.copiedId.set(null), 2000);
    });
  }

  remove(c: GrowthContent): void {
    this.api.remove(c.id).subscribe(() => this.load());
  }

  private load(): void {
    this.loading.set(true);
    this.api.library().subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
