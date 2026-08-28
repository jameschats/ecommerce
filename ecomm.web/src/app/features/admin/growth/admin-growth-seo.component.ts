import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ContentBrief, GrowthService, KeywordIdea } from '../../../core/services/growth.service';

@Component({
  selector: 'app-admin-growth-seo',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 class="text-xl font-bold text-slate-900">🔍 SEO ideas</h1>
          <p class="text-sm text-slate-500">Find what shoppers search for, then turn a keyword into a ready-to-write brief.</p>
        </div>
        <a routerLink="/admin/articles" class="text-sm text-primary hover:underline shrink-0">Blog →</a>
      </div>

      @if (locked()) {
        <div class="rounded-xl border border-violet-200 bg-violet-50 p-6 text-center">
          <div class="text-3xl">🔍</div>
          <h2 class="font-semibold text-violet-900 mt-2">SEO ideas are a plan upgrade away</h2>
          <a routerLink="/admin/billing" class="inline-block mt-3 btn-primary">See plans</a>
        </div>
      } @else {
        <div class="grid md:grid-cols-2 gap-6">
          <!-- Keyword ideas -->
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <h2 class="font-semibold text-slate-800 mb-2">Keyword ideas</h2>
            <div class="flex gap-2">
              <input [(ngModel)]="seed" name="seed" class="input flex-1" placeholder="Optional seed, e.g. cotton sarees" />
              <button type="button" (click)="getKeywords()" [disabled]="kwBusy()" class="btn-primary disabled:opacity-60 whitespace-nowrap">
                {{ kwBusy() ? '…' : 'Ideas (2 cr)' }}
              </button>
            </div>
            @if (kwError()) { <p class="text-sm text-red-600 mt-2">{{ kwError() }}</p> }
            @if (keywords().length) {
              <ul class="mt-4 divide-y divide-slate-100">
                @for (k of keywords(); track k.keyword) {
                  <li class="py-2">
                    <div class="flex items-center justify-between gap-2">
                      <span class="text-sm font-medium text-slate-800">{{ k.keyword }}</span>
                      <span class="text-[10px] px-1.5 py-0.5 rounded-full shrink-0" [class]="intentClass(k.intent)">{{ k.intent }}</span>
                    </div>
                    @if (k.note) { <p class="text-xs text-slate-500">{{ k.note }}</p> }
                    <button type="button" (click)="briefFor(k.keyword)" class="text-xs text-primary hover:underline mt-0.5">Make a brief →</button>
                  </li>
                }
              </ul>
            }
          </div>

          <!-- Content brief -->
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <h2 class="font-semibold text-slate-800 mb-2">Content brief</h2>
            <div class="flex gap-2">
              <input [(ngModel)]="keyword" name="kw" class="input flex-1" placeholder="Target keyword" />
              <button type="button" (click)="getBrief()" [disabled]="briefBusy() || !keyword.trim()" class="btn-primary disabled:opacity-60 whitespace-nowrap">
                {{ briefBusy() ? '…' : 'Brief (3 cr)' }}
              </button>
            </div>
            @if (briefError()) { <p class="text-sm text-red-600 mt-2">{{ briefError() }}</p> }
            @if (brief(); as b) {
              <div class="mt-4 text-sm">
                <div class="font-semibold text-slate-800">{{ b.suggestedTitle }}</div>
                <p class="text-slate-500 text-xs mt-0.5">{{ b.metaDescription }}</p>
                @if (b.outline.length) {
                  <div class="mt-3 font-medium text-slate-700">Outline</div>
                  <ul class="list-disc pl-5 text-slate-600">@for (h of b.outline; track h) { <li>{{ h }}</li> }</ul>
                }
                @if (b.questions.length) {
                  <div class="mt-3 font-medium text-slate-700">Questions to answer</div>
                  <ul class="list-disc pl-5 text-slate-600">@for (q of b.questions; track q) { <li>{{ q }}</li> }</ul>
                }
                <a [routerLink]="['/admin/articles/new']" [queryParams]="{ topic: b.suggestedTitle || b.targetKeyword }"
                   class="inline-block mt-4 btn-primary text-sm">Write this article →</a>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class AdminGrowthSeoComponent implements OnInit {
  private readonly api = inject(GrowthService);

  readonly locked = signal(false);
  readonly keywords = signal<KeywordIdea[]>([]);
  readonly brief = signal<ContentBrief | null>(null);
  readonly kwBusy = signal(false);
  readonly briefBusy = signal(false);
  readonly kwError = signal<string | null>(null);
  readonly briefError = signal<string | null>(null);

  seed = '';
  keyword = '';

  ngOnInit(): void {
    // Cheap gate probe: the types call 402s on a non-growth plan.
    this.api.types().subscribe({ error: (e) => { if (e?.status === 402) this.locked.set(true); } });
  }

  getKeywords(): void {
    this.kwBusy.set(true);
    this.kwError.set(null);
    this.api.keywordIdeas(this.seed.trim() || null, null).subscribe({
      next: (k) => { this.keywords.set(k); this.kwBusy.set(false); },
      error: (e) => { this.kwBusy.set(false); this.kwError.set(this.msg(e)); },
    });
  }

  briefFor(kw: string): void { this.keyword = kw; this.getBrief(); }

  getBrief(): void {
    if (!this.keyword.trim()) return;
    this.briefBusy.set(true);
    this.briefError.set(null);
    this.api.contentBrief(this.keyword.trim(), null).subscribe({
      next: (b) => { this.brief.set(b); this.briefBusy.set(false); },
      error: (e) => { this.briefBusy.set(false); this.briefError.set(this.msg(e)); },
    });
  }

  intentClass(intent: string): string {
    switch (intent) {
      case 'transactional': return 'bg-emerald-100 text-emerald-700';
      case 'commercial': return 'bg-blue-100 text-blue-700';
      default: return 'bg-slate-100 text-slate-600';
    }
  }

  private msg(e: { status?: number; error?: { message?: string } }): string {
    return e?.status === 402 ? "You're out of AI credits — top up to keep going." : e?.error?.message ?? 'Could not fetch that just now.';
  }
}
