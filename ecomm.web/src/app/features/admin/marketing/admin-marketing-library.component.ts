import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ChannelPref, LibraryItem, MarketingPlanSettings, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * Creative Library — every poster/text post the Studio has ever made, wherever it came from ("This
 * week" or the standalone Poster Studio), in one browsable place. Answers "after creating a poster,
 * where does it go?" — and lets a merchant assign a channel to anything not yet scheduled, right here.
 */
@Component({
  selector: 'app-admin-marketing-library',
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Creative Library</h1>
      <p class="text-sm text-slate-500 mb-5">Everything the studio has generated — from This week or Poster Studio. Assign a channel to anything not yet scheduled.</p>

      <div class="flex gap-1.5 mb-5">
        @for (f of filters; track f.value) {
          <button type="button" (click)="setFilter(f.value)" class="text-xs px-3 py-1 rounded-full border"
                  [class]="filter() === f.value ? 'bg-slate-900 text-white border-slate-900' : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-50'">
            {{ f.label }}
          </button>
        }
      </div>

      @if (loading()) {
        <p class="text-sm text-slate-500">Loading…</p>
      } @else if (items().length === 0) {
        <div class="text-sm text-slate-600 border border-dashed border-slate-300 rounded-xl p-6 text-center">
          Nothing here yet. Create something in
          <a routerLink="/admin/marketing/poster" class="text-teal-700 font-medium hover:underline">Poster Studio</a>
          or confirm <a routerLink="/admin/marketing/plan/review" class="text-teal-700 font-medium hover:underline">This week</a>'s plan.
        </div>
      } @else {
        <div class="grid sm:grid-cols-2 gap-4">
          @for (item of items(); track item.creativeId) {
            <div class="bg-white border border-slate-200 rounded-xl p-4 flex gap-3">
              @if (item.type === 'poster' && item.mediaUrl) {
                <img [src]="item.mediaUrl" alt="Poster" class="w-20 h-20 rounded-lg border border-slate-200 object-cover shrink-0" />
              }
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs font-semibold px-2 py-0.5 rounded-full shrink-0"
                        [class]="item.type === 'poster' ? 'bg-purple-100 text-purple-700' : 'bg-sky-100 text-sky-700'">{{ item.type }}</span>
                  <span class="text-xs text-slate-400 ml-auto shrink-0">{{ formatDate(item.createdAt) }}</span>
                </div>
                @if (item.body) { <p class="text-sm text-slate-600 line-clamp-3">{{ item.body }}</p> }

                @if (item.type === 'poster') {
                  <div class="flex items-center gap-3 mt-1.5">
                    @if (item.editable) {
                      <a [routerLink]="['/admin/marketing/poster']" [queryParams]="{ edit: item.creativeId }" class="text-xs font-medium text-teal-700 hover:underline">Edit</a>
                    }
                    <button type="button" (click)="duplicate(item)" [disabled]="duplicating() === item.creativeId" class="text-xs font-medium text-slate-500 hover:text-slate-700 disabled:opacity-50">
                      {{ duplicating() === item.creativeId ? 'Duplicating…' : 'Duplicate' }}
                    </button>
                  </div>
                }

                @if (item.channels.length) {
                  <div class="flex flex-wrap gap-1.5 mt-2">
                    @for (ch of item.channels; track ch) { <span class="text-xs px-2 py-0.5 rounded-full bg-green-50 text-green-700">{{ ch }}</span> }
                  </div>
                } @else {
                  <div class="mt-2.5 pt-2.5 border-t border-slate-100">
                    @if (availableChannels(item).length === 0) {
                      <p class="text-xs text-slate-400">Connect a channel that allows {{ item.type }} to schedule this.</p>
                    } @else {
                      <div class="flex flex-wrap items-center gap-2">
                        @for (ch of availableChannels(item); track ch.platform) {
                          <label class="flex items-center gap-1 text-xs text-slate-600 border border-slate-200 rounded-full px-2 py-0.5 cursor-pointer"
                                 [class.bg-teal-50]="isSelected(item.creativeId, ch.platform)" [class.border-teal-300]="isSelected(item.creativeId, ch.platform)">
                            <input type="checkbox" class="sr-only" [checked]="isSelected(item.creativeId, ch.platform)" (change)="toggleChannel(item.creativeId, ch.platform)" />
                            {{ ch.displayName }}
                          </label>
                        }
                        <button type="button" (click)="schedule(item)" [disabled]="scheduling() === item.creativeId || !hasSelection(item.creativeId)"
                                class="text-xs font-medium text-teal-700 hover:underline disabled:opacity-50">
                          {{ scheduling() === item.creativeId ? 'Scheduling…' : 'Schedule' }}
                        </button>
                      </div>
                    }
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  imports: [RouterLink],
})
export class AdminMarketingLibraryComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);

  readonly items = signal<LibraryItem[]>([]);
  readonly settings = signal<MarketingPlanSettings | null>(null);
  readonly loading = signal(true);
  readonly filter = signal<'' | 'text' | 'poster'>('');
  readonly scheduling = signal<number | null>(null);
  readonly duplicating = signal<number | null>(null);
  private readonly selectedChannels = new Map<number, Set<string>>();

  readonly filters: { value: '' | 'text' | 'poster'; label: string }[] = [
    { value: '', label: 'All' },
    { value: 'poster', label: 'Posters' },
    { value: 'text', label: 'Text' },
  ];

  ngOnInit(): void {
    this.load();
    this.api.getPlanSettings().subscribe((s) => this.settings.set(s));
  }

  private load(): void {
    this.loading.set(true);
    this.api.listLibrary(this.filter() || undefined).subscribe({
      next: (items) => { this.items.set(items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  setFilter(v: '' | 'text' | 'poster'): void { this.filter.set(v); this.load(); }

  availableChannels(item: LibraryItem): ChannelPref[] {
    const s = this.settings();
    if (!s) return [];
    return s.channels.filter((c) => c.enabled && (item.type === 'poster' ? c.allowPoster : c.allowText));
  }

  isSelected(creativeId: number, platform: string): boolean {
    return this.selectedChannels.get(creativeId)?.has(platform) ?? false;
  }
  hasSelection(creativeId: number): boolean {
    return (this.selectedChannels.get(creativeId)?.size ?? 0) > 0;
  }
  toggleChannel(creativeId: number, platform: string): void {
    const set = this.selectedChannels.get(creativeId) ?? new Set<string>();
    if (set.has(platform)) set.delete(platform); else set.add(platform);
    this.selectedChannels.set(creativeId, set);
  }

  schedule(item: LibraryItem): void {
    const channels = Array.from(this.selectedChannels.get(item.creativeId) ?? []);
    if (channels.length === 0) return;
    this.scheduling.set(item.creativeId);
    this.api.scheduleItem(item.itemId, channels).subscribe({
      next: () => {
        this.scheduling.set(null);
        this.selectedChannels.delete(item.creativeId);
        this.items.set(this.items().map((i) => i.creativeId === item.creativeId ? { ...i, channels: [...i.channels, ...channels] } : i));
      },
      error: () => this.scheduling.set(null),
    });
  }

  duplicate(item: LibraryItem): void {
    this.duplicating.set(item.creativeId);
    this.api.duplicatePoster(item.creativeId).subscribe({
      next: (r) => {
        this.duplicating.set(null);
        this.items.set([
          { creativeId: r.creativeId, itemId: r.itemId, type: 'poster', body: r.caption, mediaUrl: r.mediaUrl, productId: item.productId, createdAt: new Date().toISOString(), channels: [], editable: true },
          ...this.items(),
        ]);
      },
      error: () => this.duplicating.set(null),
    });
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }
}
