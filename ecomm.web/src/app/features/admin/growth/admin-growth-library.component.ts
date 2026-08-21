import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { GrowthContent, GrowthService } from '../../../core/services/growth.service';
import { PagedResult } from '../../../core/models/api-response.model';

/** Content Library (v4 Phase 4 Track A hardening) — filterable by channel/campaign/date on top of
 * what was already a flat, unfiltered list, plus an "edited" indicator + view-original toggle so a
 * merchant can tell "generated" from "hand-edited" from "approved," per the roadmap's actual ask. */
@Component({
  selector: 'app-admin-growth-library',
  imports: [DatePipe, RouterLink, FormsModule],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between gap-4 mb-4">
        <div>
          <h1 class="text-xl font-bold text-slate-900">Content library</h1>
          <p class="text-sm text-slate-500">Everything you've generated. Copy, reuse or clean up.</p>
        </div>
        <a routerLink="/admin/growth" class="btn-primary shrink-0">✨ Generate</a>
      </div>

      <div class="bg-white border border-slate-200 rounded-xl p-3 mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label class="lbl">Channel</label>
          <select [(ngModel)]="contentType" name="contentType" (ngModelChange)="applyFilters()" class="input">
            <option [ngValue]="null">All channels</option>
            @for (key of channelKeys; track key) { <option [ngValue]="key">{{ label(key) }}</option> }
          </select>
        </div>
        <div>
          <label class="lbl">From</label>
          <input type="date" [(ngModel)]="from" name="from" (ngModelChange)="applyFilters()" class="input" />
        </div>
        <div>
          <label class="lbl">To</label>
          <input type="date" [(ngModel)]="to" name="to" (ngModelChange)="applyFilters()" class="input" />
        </div>
        @if (campaignId()) {
          <div class="flex items-center gap-2 text-sm text-primary bg-blue-50 rounded-lg px-3 py-2">
            Filtered to one campaign
            <button type="button" (click)="clearCampaignFilter()" class="text-primary/70 hover:text-primary">×</button>
          </div>
        }
        @if (contentType || from || to || campaignId()) {
          <button type="button" (click)="clearFilters()" class="text-sm text-slate-500 hover:underline">Clear filters</button>
        }
      </div>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if ((result()?.items ?? []).length === 0) {
        <div class="bg-white border border-slate-200 rounded-xl p-8 text-center">
          <div class="text-3xl">🗂️</div>
          <p class="text-slate-600 font-medium mt-2">Nothing matches</p>
          <a routerLink="/admin/growth" class="text-sm text-primary hover:underline mt-1 inline-block">Generate your first post →</a>
        </div>
      } @else {
        <div class="space-y-3">
          @for (c of result()?.items ?? []; track c.id) {
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0 flex items-center gap-1.5 flex-wrap">
                  <span class="text-[11px] px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">{{ label(c.contentType) }}</span>
                  @if (c.status === 'Kept') { <span class="text-[11px] px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700">Approved</span> }
                  @if (c.wasEdited) { <span class="text-[11px] px-2 py-0.5 rounded-full bg-amber-100 text-amber-700">Edited</span> }
                  <span class="text-xs text-slate-400">{{ c.language }} · {{ c.createdAt | date:'dd MMM, HH:mm' }}</span>
                </div>
                <div class="flex gap-2 shrink-0">
                  <button type="button" (click)="copy(c)" class="text-sm text-primary hover:underline">{{ copiedId() === c.id ? 'Copied ✓' : 'Copy' }}</button>
                  <button type="button" (click)="remove(c)" class="text-sm text-red-600 hover:underline">Delete</button>
                </div>
              </div>
              @if (c.title) { <div class="text-sm font-medium text-slate-800 mt-2">{{ c.title }}</div> }
              <p class="text-sm text-slate-600 mt-1 whitespace-pre-line">{{ c.body }}</p>

              @if (c.wasEdited) {
                <button type="button" (click)="toggleOriginal(c.id)" class="text-xs text-slate-400 hover:text-slate-600 mt-2">
                  {{ showingOriginal() === c.id ? 'Hide original' : 'View original AI draft' }}
                </button>
                @if (showingOriginal() === c.id) {
                  <div class="mt-2 border-t border-dashed border-slate-200 pt-2">
                    @if (c.originalTitle) { <div class="text-sm font-medium text-slate-500">{{ c.originalTitle }}</div> }
                    <p class="text-sm text-slate-400 whitespace-pre-line">{{ c.originalBody }}</p>
                  </div>
                }
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminGrowthLibraryComponent implements OnInit {
  private readonly api = inject(GrowthService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly result = signal<PagedResult<GrowthContent> | null>(null);
  readonly copiedId = signal<number | null>(null);
  readonly campaignId = signal<number | null>(null);
  readonly showingOriginal = signal<number | null>(null);

  contentType: string | null = null;
  from: string | null = null;
  to: string | null = null;

  private readonly labels: Record<string, string> = {
    'instagram-caption': 'Instagram', 'facebook-post': 'Facebook', 'whatsapp': 'WhatsApp',
    'email': 'Email', 'product-description': 'Product', 'festival-offer': 'Festival', 'google-ads': 'Google Ads',
  };
  readonly channelKeys = Object.keys(this.labels);

  ngOnInit(): void {
    const campaignParam = this.route.snapshot.queryParamMap.get('campaignId');
    if (campaignParam) this.campaignId.set(Number(campaignParam));
    this.load();
  }

  label(key: string): string { return this.labels[key] ?? key; }

  applyFilters(): void { this.load(); }

  clearCampaignFilter(): void { this.campaignId.set(null); this.load(); }

  clearFilters(): void {
    this.contentType = null; this.from = null; this.to = null; this.campaignId.set(null);
    this.load();
  }

  toggleOriginal(id: number): void {
    this.showingOriginal.set(this.showingOriginal() === id ? null : id);
  }

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
    this.api.library({
      contentType: this.contentType, campaignId: this.campaignId(), from: this.from, to: this.to,
    }).subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
